namespace CandidateParsingAgilityPack.Model
{
    using Newtonsoft.Json;

    internal class FreeBaseConsumerCompanyBrands
    {
        [JsonProperty(PropertyName = "brand")]
        public string Brand { get; set; }
    }
}