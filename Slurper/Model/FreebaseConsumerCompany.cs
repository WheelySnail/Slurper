namespace Slurper.Model
{
    #region Using Directives

    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    internal class FreebaseConsumerCompany
    {
        [JsonProperty(PropertyName = "brands")]
        public List<FreeBaseConsumerCompanyBrands> Brands { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "products")]
        public List<FreebaseConsumerCompanyProducts> Products { get; set; }
    }
}