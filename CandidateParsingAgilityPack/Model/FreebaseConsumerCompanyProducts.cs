namespace CandidateParsingAgilityPack.Model
{
    using Newtonsoft.Json;

    internal class FreebaseConsumerCompanyProducts
    {
        [JsonProperty(PropertyName = "consumer_product")]
        public string Product { get; set; }

    }
}