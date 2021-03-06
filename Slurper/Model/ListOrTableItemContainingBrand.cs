namespace Slurper.Model
{
    using System.Collections.Generic;

    internal class ListOrTableItem 
    {
        public string ItemHtml { get; set; }

        public int ItemWordCount { get; set; }

        public List<string> WordsInItem { get; set; }

        public string KnownBrand { get; set; }

        public bool ContainsBrandOnly { get; set; }

        public string ItemInnerText { get; set; }

        public string ItemHtmlWithoutBrand { get; set; }
    }
}