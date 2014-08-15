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
        public List<string> BrandNames
        {
            get
            {
                return new List<string>() { BrandName };
            }
        }
       

        public string OwnerId { get; set; }

        [JsonProperty(PropertyName = "company")]
        public string OwnerName { get; set; }

        [JsonIgnore]
        public List<string> OwnerNames
        {
            get
            {
                return new List<string>() { OwnerName };
            }
        }

        public string AsJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}