namespace Slurper.Model
{
    #region Using Directives

    using System;
    using System.Collections.Generic;

    using numl.Model;

    #endregion

    public class Candidate
    {
        [Label]
        public bool CompanyBrandRelationship { get; set; }

        [Feature]
        public String CandidateHtml { get; set; }

        [Feature]
        public string PreviousContent { get; set; }

        [Feature]
        public int CandidateHtmlWordCount { get; set; }

        [Feature]
        public int PreviousContentWordCount { get; set; }

        [Feature]
        public bool ContainsMultipleBrands { get; set; }

        [Feature]
        public bool ItemsContainBrandOnly { get; set; }

        [Feature]
        public bool DomainOrPageTitleContainsOwner { get; set; }

        [Feature]
        public bool PreviousContentContainsPotentialOwner { get; set; }

        [Feature]
        public bool CandidateHtmlContainsPotentialOwner { get; set; }

        [Feature]
        public bool IsListSegment { get; set; }

        [Feature]
        public bool IsTableSegment { get; set; }

        public bool IsItemLevelCandidate { get; set; }

        public string KnownBrand { get; set; }

        public List<String> KnownBrands { get; set; }

        public CompanyAndBrands KnownCompanyAndBrands { get; set; }

        public string KnownCompanyName { get; set; }

        public String PageTitle { get; set; }

        public string Uri { get; set; }

        public List<string> WordsInPreviousContent { get; set; }

        public List<string> WordsInCandidateHtml { get; set; }

    }
}