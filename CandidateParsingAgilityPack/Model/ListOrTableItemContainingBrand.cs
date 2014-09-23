namespace CandidateParsingAgilityPack.Model
{
    using System.Collections.Generic;

    internal class ListOrTableItemContainingBrand 
    {
        public string ItemHtml { get; set; }

        public int ItemWordCount { get; set; }

        public List<string> WordsInItem { get; set; }

        public string KnownBrand { get; set; }

        public bool ContainsBrandOnly { get; set; }
    }
}