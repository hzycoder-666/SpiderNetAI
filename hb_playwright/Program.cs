using System.Threading.Channels;
using hb_playwright;
using Microsoft.Playwright;

public class Program
{
    const string StartUrl = "https://ai-bot.cn";
    public static async Task Main(string[] args)
    {
        var webcrawler = new WebCrawler(StartUrl);
        await webcrawler.StartCrawlingAsync();
    }
}