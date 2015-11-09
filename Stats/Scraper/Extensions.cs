using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scraper
{
    public static class Extensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;

            if (dict.TryGetValue(key, out value))
                return value;
            else
                return defaultValue;
        }

        public static HtmlNode SelectFirstNodeOrNull(this HtmlNode parent, string xpath)
        {
            HtmlNodeCollection nodes = parent.SelectNodes(xpath);

            if (nodes != null)
                return nodes.FirstOrDefault();
            else
                return null;
        }
    }
}
