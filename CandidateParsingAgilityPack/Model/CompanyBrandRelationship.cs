namespace CandidateParsingAgilityPack.Model
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class CompanyBrandRelationship
    {
        [JsonProperty(PropertyName = "id")]
        public string RelationshipId { get; set; }

        public string BrandId { get; set; }

        [JsonProperty(PropertyName = "brand")]
        public string BrandName { get; set; }

        [JsonIgnore]
        public List<string> BrandNames { get; set; }

        public string OwnerId { get; set; }

        [JsonProperty(PropertyName = "company")]
        public string OwnerName { get; set; }

        [JsonIgnore]
        public List<string> OwnerNames { get; set; }

        public string AsJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}