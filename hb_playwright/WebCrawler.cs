using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace hb_playwright
{
    internal class WebCrawler
    {
        private readonly CancellationTokenSource _cts;
        private readonly DistinctChannel<string> _urlChannel;
        private readonly DistinctChannel<ScrapedLink> _resultChannel;
        private readonly string _startUrl;

        public WebCrawler(string startUrl)
        {
            // URL队列（待抓取）
            _urlChannel = new DistinctChannel<string>();

            // 结果队列（已抓取）
            _resultChannel = new DistinctChannel<ScrapedLink>();

            _cts = new CancellationTokenSource();

            _startUrl = startUrl;
        }

        public async Task StartCrawlingAsync()
        {
            var seedUrls = new[] { _startUrl };
            // 1. 添加种子URL
            foreach (var url in seedUrls)
            {
                _urlChannel.TryWrite(url, string.Empty);
            }

            // 2. 添加多个爬虫工作器
            var concurrency = 4;
            var crawlers = Enumerable.Range(1, concurrency)
                .Select(x => Task.Run(() => CrawlWorkerAsync($"工作器{x}", _cts.Token)))
                .ToArray();

            // 3. 启动结果处理器
            var processor = Task.Run(() => ProcessResultsAsync(_cts.Token));

            Console.WriteLine("按回车停止爬取...");
            // 等待用户按回车停止
            await Task.Run(() => Console.ReadLine());
            _cts.Cancel();

            // 等待所有工作器完成
            await Task.WhenAll(crawlers);

            // 关闭写端，通知结果处理器不再有新数据
            _urlChannel.CompleteWriter();
            _resultChannel.CompleteWriter();

            // 等待结果处理完成
            await processor;
        }

        private async Task CrawlWorkerAsync(string workerName, CancellationToken token)
        {
            Console.WriteLine($"{workerName} 启动");
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions()
            {
                Headless = true
            });
            var context = await browser.NewContextAsync(new BrowserNewContextOptions()
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36"
            });
            while (!token.IsCancellationRequested)
            {
                if (_urlChannel.TryRead(out var key, out var meta))
                {
                    var url = key;
                    Console.WriteLine($"{workerName} 抓取: {url}");
                    await new ScrapeTask(url, _urlChannel, _resultChannel, context, _startUrl).RunAsync();
                }
                else
                {
                    try { await Task.Delay(200, token); } catch { break; }
                }
            }
        }

        private async Task ProcessResultsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested || _resultChannel.Count > 0)
            {
                if (_resultChannel.TryRead(out var key, out var result))
                {
                    if (result == null) continue;
                    var content = string.Format("{0}\t{1}\t{2}", result.Title, result.Href, result.Desc) + Environment.NewLine;
                    File.AppendAllText("AI_bot_urls.txt", content, Encoding.UTF8);
                }
                else
                {
                    try { await Task.Delay(200, token); } catch { break; }
                }
            }
        }
    }
}