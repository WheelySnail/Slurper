namespace CandidateParsingAgilityPack.Model
{
    #region Using Directives

    using HtmlAgilityPack;

    #endregion

    internal class InitialCandidate
    {
        public HtmlNode Node { get; set; }

        public string Type { get; set; }
    }
}