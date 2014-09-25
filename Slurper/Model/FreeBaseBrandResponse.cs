namespace Slurper.Model
{
    #region Using Directives

    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    internal class FreeBaseBrandResponse
    {
        [JsonProperty(PropertyName = "result")]
        public List<FreebaseBrand> Brands { get; set; }
    }
}