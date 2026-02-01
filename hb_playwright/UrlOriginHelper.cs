using System;
using System.Collections.Generic;
using System.Text;

namespace hb_playwright
{
    internal static class UrlOriginHelper
    {
        /// <summary>
        /// 判断两个 URL 是否同源
        /// </summary>
        /// <param name="url1">第一个 URL</param>
        /// <param name="url2">第二个 URL</param>
        /// <returns>是否同源</returns>
        public static bool IsSameOrigin(string url1, string url2)
        {
            if (string.IsNullOrWhiteSpace(url1) || string.IsNullOrWhiteSpace(url2))
                return false;

            try
            {
                var uri1 = new Uri(url1, UriKind.Absolute);
                var uri2 = new Uri(url2, UriKind.Absolute);

                // 比较协议、主机和端口
                return string.Equals(uri1.Scheme, uri2.Scheme, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(uri1.Host, uri2.Host, StringComparison.OrdinalIgnoreCase)
                    && uri1.Port == uri2.Port;
            }
            catch (UriFormatException)
            {
                // URL 格式无效
                return false;
            }
        }

        /// <summary>
        /// 判断 href 是否与基础 URL 同源
        /// </summary>
        /// <param name="baseUrl">基础 URL</param>
        /// <param name="href">要检查的链接</param>
        /// <returns>是否同源</returns>
        public static bool IsSameOriginWithBase(string baseUrl, string href)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(href))
                return false;

            try
            {
                var baseUri = new Uri(baseUrl, UriKind.Absolute);
                Uri hrefUri;

                // 处理相对路径
                if (!Uri.TryCreate(href, UriKind.Absolute, out hrefUri))
                {
                    // 尝试将相对路径转换为绝对路径
                    hrefUri = new Uri(baseUri, href);
                }

                // 比较源
                return string.Equals(baseUri.Scheme, hrefUri.Scheme, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(baseUri.Host, hrefUri.Host, StringComparison.OrdinalIgnoreCase)
                    && baseUri.Port == hrefUri.Port;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }
    }
}
