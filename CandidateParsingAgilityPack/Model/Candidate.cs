namespace CandidateParsingAgilityPack.Model
{
    using System;
    using System.Collections.Generic;

    using numl.Model;

    public class Candidate
    {
        [Label]
        public bool CompanyBrandRelationship { get; set; }

        [Feature]
        public bool containsMultipleBrands { get; set; }

        [Feature]
        public String PageTitle { get; set; }

        public List<string> KnownCompany { get; set; }

        public string KnownBrand { get; set; }

        [Feature]
        public bool BrandNameIsSimilarToCompanyName { get; set; }

        // Does it make sense to have this here? 
        public CompanyAndBrands KnownCompanyAndBrands { get; set; }

        [Feature]
        public String DomainName { get; set; }

        public List<String> UriSegments { get; set; }

        [Feature]
        public String NumberOfTokens { get; set; }

        [Feature]
        public String CandidateHtml { get; set; }

        [Feature]
        public bool IsListSegment { get; set; }

        [Feature]
        public bool IsTableSegment { get; set; }

        public List<String> NamedEntities { get; set; }

        public string Uri { get; set; }

        [Feature]
        public bool DomainOrPageTitleContainsOwner { get; set; }

        [Feature]
        public string PreviousContent { get; set; }

        public List<String> KnownBrands { get; set; }

        public bool IsItemLevelCandidate  { get; set; }
    }
}