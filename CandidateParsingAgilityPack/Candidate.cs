namespace CandidateParsingAgilityPack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Security.Policy;

    public class Candidate
    {
        public String PageTitle { get; set; }

        public String KnownCompany { get; set; }

        public String KnownCompanyBrandRelationship { get; set; }

        public String DomainName { get; set; }

        public List<String> UriSegments { get; set; }

        public String NumberOfTokens { get; set; }

        public String CandidateHtmlAndText { get; set; }

        public bool IsListSegment { get; set; }

        public bool IsTableSegment { get; set; }

        public bool IsTextSegment { get; set; }

        public List<String> NamedEntities { get; set; }

        public string Uri { get; set; }
    }
}