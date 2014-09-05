namespace CandidateParsingAgilityPack.Model
{
    #region Using Directives

    using Newtonsoft.Json;

    #endregion

    internal class FreebaseConsumerCompanyProducts
    {
        [JsonProperty(PropertyName = "consumer_product")]
        public string Product { get; set; }
    }
}