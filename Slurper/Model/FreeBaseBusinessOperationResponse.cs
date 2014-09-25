namespace Slurper.Model
{
    #region Using Directives

    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    internal class FreeBaseBusinessOperationResponse
    {
        [JsonProperty(PropertyName = "result")]
        public List<FreebaseBusinessOperation> Businesses { get; set; }
    }
}