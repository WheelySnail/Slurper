namespace CandidateParsingAgilityPack.Model
{
    #region Using Directives

    using Newtonsoft.Json;

    #endregion

    public class FreebaseCompanyBrandRelationship
    {
        [JsonProperty(PropertyName = "brand")]
        public string Brand { get; set; }

        [JsonProperty(PropertyName = "company")]
        public string Company { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string RelationshipId { get; set; }
    }
}