namespace CandidateParsingAgilityPack.Model
{
    #region Using Directives

    using System;
    using System.Collections.Generic;

    using numl.Model;

    #endregion

    public class Candidate
    {
        //[Feature]
        //public bool BrandNameIsSimilarToCompanyName { get; set; }

        [Feature]
        public String CandidateHtml { get; set; }

        [Label]
        public bool CompanyBrandRelationship { get; set; }

        [Feature]
        public bool ContainsMultipleBrands { get; set; }

        //[Feature]
        //public String DomainName { get; set; }

        [Feature]
        public bool DomainOrPageTitleContainsOwner { get; set; }

        public bool IsItemLevelCandidate { get; set; }

        [Feature]
        public bool IsListSegment { get; set; }

        [Feature]
        public bool IsTableSegment { get; set; }

        public string KnownBrand { get; set; }

        public List<String> KnownBrands { get; set; }

        public CompanyAndBrands KnownCompanyAndBrands { get; set; }

        public List<string> KnownCompanyNames { get; set; }

        //public List<String> NamedEntities { get; set; }

        //[Feature]
        //public String NumberOfTokens { get; set; }

        [Feature]
        public String PageTitle { get; set; }

        [Feature]
        public string PreviousContent { get; set; }

        public string Uri { get; set; }

        //public List<String> UriSegments { get; set; }
    }
}