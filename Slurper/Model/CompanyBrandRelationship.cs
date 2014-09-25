namespace Slurper.Model
{
    #region Using Directives

    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    public class CompanyAndBrands
    {
        public string BrandId { get; set; }

        public List<string> BrandNames { get; set; }

        public string CompanyId { get; set; }

        public string CompanyName { get; set; }

        public string RelationshipId { get; set; }

        public string AsJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}