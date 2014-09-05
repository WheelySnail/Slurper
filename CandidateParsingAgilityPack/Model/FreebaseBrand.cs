namespace CandidateParsingAgilityPack.Model
{
    #region Using Directives

    using Newtonsoft.Json;

    #endregion

    internal class FreebaseBrand
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }
    }
}