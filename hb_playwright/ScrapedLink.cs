using System;
using System.Collections.Generic;
using System.Text;

namespace hb_playwright
{
    internal record ScrapedLink
    {
        public string Title { get; init; }
        public string Href { get; init; }
        public string? Desc { get; init; }
        public ScrapedLink(string title, string href, string? desc)
        {
            Title = title;
            Href = href;
            Desc = desc;
        }
    }
}
