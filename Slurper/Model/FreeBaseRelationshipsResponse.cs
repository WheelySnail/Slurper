namespace Slurper.Model
{
    #region Using Directives

    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    internal class FreeBaseRelationshipsResponse
    {
        [JsonProperty(PropertyName = "result")]
        public List<FreebaseCompanyBrandRelationship> Relationships { get; set; }
    }
}