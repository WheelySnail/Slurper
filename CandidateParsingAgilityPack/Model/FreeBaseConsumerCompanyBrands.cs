namespace CandidateParsingAgilityPack.Model
{
    #region Using Directives

    using Newtonsoft.Json;

    #endregion

    internal class FreeBaseConsumerCompanyBrands
    {
        [JsonProperty(PropertyName = "brand")]
        public string Brand { get; set; }
    }
}