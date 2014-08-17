namespace CandidateParsingAgilityPack.Model
{
    using Newtonsoft.Json;

    public class FreebaseCompanyBrandRelationship
    {
        [JsonProperty(PropertyName = "company")]
        public string Company { get; set; }

        [JsonProperty(PropertyName = "brand")]
        public string Brand { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string RelationshipId { get; set; }
    }
}