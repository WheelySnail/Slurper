namespace CandidateParsingAgilityPack.Model
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    internal class FreebaseConsumerCompany     {   
        
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "brands")]
        public List<FreeBaseConsumerCompanyBrands> Brands { get; set; }

        [JsonProperty(PropertyName = "products")]
        public List<FreebaseConsumerCompanyProducts> Products { get; set; }
    }
}