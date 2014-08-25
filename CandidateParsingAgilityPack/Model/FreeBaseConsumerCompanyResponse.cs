using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CandidateParsingAgilityPack.Model
{
    using Newtonsoft.Json;

    internal class FreeBaseConsumerCompanyResponse
    {
        [JsonProperty(PropertyName = "result")]
        public List<FreebaseConsumerCompany> Companies { get; set; }
    }
}