namespace CandidateParsingAgilityPack.Model
{
    using System;
    using System.Collections.Generic;

    public class Candidate
    {
        public bool containsMultipleBrands { get; set; }

        public String PageTitle { get; set; }

        public List<string> KnownCompany { get; set; }

        public string KnownBrand { get; set; }

        public bool BrandNameIsSimilarToCompanyName { get; set; }

        // Does it make sense to have this here? 
        public CompanyAndBrands KnownCompanyAndBrands { get; set; }

        public String DomainName { get; set; }

        public List<String> UriSegments { get; set; }

        public String NumberOfTokens { get; set; }

        public String CandidateHtml { get; set; }

        public bool IsListSegment { get; set; }

        public bool IsTableSegment { get; set; }

        public List<String> NamedEntities { get; set; }

        public string Uri { get; set; }

        public bool DomainOrPageTitleContainsOwner { get; set; }

        public string PreviousContent { get; set; }
    }
}