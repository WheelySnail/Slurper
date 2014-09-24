namespace CandidateParsingAgilityPack.Model
{
    #region Using Directives

    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    internal class FreebaseBusinessOperation
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }
}