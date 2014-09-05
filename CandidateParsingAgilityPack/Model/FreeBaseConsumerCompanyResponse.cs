namespace CandidateParsingAgilityPack.Model
{
    #region Using Directives

    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    internal class FreeBaseConsumerCompanyResponse
    {
        [JsonProperty(PropertyName = "result")]
        public List<FreebaseConsumerCompany> Companies { get; set; }
    }
}