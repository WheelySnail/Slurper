namespace CandidateParsingAgilityPack.Model
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    internal class FreeBaseRelationshipsResponse
    {
        [JsonProperty(PropertyName = "result")]
        public List<FreebaseCompanyBrandRelationship> Relationships { get; set; }
    }
}