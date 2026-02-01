using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace hb_playwright
{
    internal class ScrapeTask
    {
        public string Url { get; init; }
        public DistinctChannel<string> UrlChannel { get; init; }
        public DistinctChannel<ScrapedLink> ResultChannel { get; init; }
        public IBrowserContext Context { get; init; }
        private string StartUrl { get; init; }
        public ScrapeTask(
            string url,
            DistinctChannel<string> urlChannel,
            DistinctChannel<ScrapedLink> resultChannel,
            IBrowserContext context,
            string startUrl)
        {
            Url = url;
            UrlChannel = urlChannel;
            ResultChannel = resultChannel;
            Context = context;
            StartUrl = startUrl;
        }

        public async Task RunAsync()
        {
            var page = await Context.NewPageAsync();
            try
            {
                await page.GotoAsync(Url, new PageGotoOptions()
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30_000
                });
                var scrapedLinks = await AnalyzePageMeta(page) ?? new List<ScrapedLink>();

                foreach (var item in scrapedLinks)
                {
                    if (item.Href.Contains("/sites/"))
                    {
                        await ResultChannel.EnqueueAsync(item.Href + "", item);
                    } else if (item.Href.Contains("#") || item.Href.Contains("/favorites/"))
                    {
                        var href = item.Href;
                        if (item.Href.StartsWith("#"))
                        {
                            href = StartUrl + item.Href;
                        }
                        // 将待抓取 URL 去重入队
                        await UrlChannel.EnqueueAsync(href, string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"抓取 {Url} 失败: {ex.Message}");
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        private async Task<List<ScrapedLink>?> AnalyzePageMeta(IPage page)
        {
            var anchors = await page.QuerySelectorAllAsync("a[href]");
            var list = new List<ScrapedLink>();

            foreach (var a in anchors)
            {
                var href = (await a.GetAttributeAsync("href"))?.Trim() ?? string.Empty;
                if (
                    string.IsNullOrEmpty(href)
                    || href == "#"
                    || href.StartsWith("javascript:")
                    || !UrlOriginHelper.IsSameOriginWithBase(StartUrl, href))
                {
                    continue;
                }

                string? title = null;
                var strong = await a.QuerySelectorAsync("strong");
                if (strong != null)
                {
                    title = (await strong.InnerTextAsync())?.Trim();
                }
                if (string.IsNullOrEmpty(title))
                {
                    title = (await a.InnerTextAsync())?.Trim();
                }

                var p = await a.QuerySelectorAsync("p");
                string? desc = p != null ? (await p.InnerTextAsync())?.Trim() : null;

                if (!string.IsNullOrEmpty(title))
                {
                    Console.WriteLine($"【{title}】\t{href}\t【{desc}】");
                    list.Add(new ScrapedLink(title, href, desc));
                }
            }

            return list.Count > 0 ? list : null;
        }
    }
}
