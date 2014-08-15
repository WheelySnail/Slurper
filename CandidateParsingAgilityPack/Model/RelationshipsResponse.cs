namespace CandidateParsingAgilityPack
{
    using System.Collections.Generic;

    using CandidateParsingAgilityPack.Model;

    using Newtonsoft.Json;

    internal class RelationshipsResponse
    {
        [JsonProperty(PropertyName = "result")]
        public List<CompanyBrandRelationship> Relationships { get; set; }
    }
}