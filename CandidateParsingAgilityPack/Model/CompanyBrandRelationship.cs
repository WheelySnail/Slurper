namespace CandidateParsingAgilityPack.Model
{
    using System.Collections.Generic;

    public class CompanyBrandRelationship
    {
        public string BrandId { get; set; }

        public List<string> BrandNames { get; set; }

        public string OwnerId { get; set; }

        public List<string> OwnerNames { get; set; } 
    }
}