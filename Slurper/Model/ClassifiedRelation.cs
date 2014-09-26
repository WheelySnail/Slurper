namespace Slurper.Model
{
    #region Using Directives

    using System;
    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    internal class ClassifiedRelation
    {
        [JsonProperty(PropertyName = "isRelation")]
        public bool IsRelation { get; set; }

        [JsonProperty(PropertyName = "company")]
        public string Company { get; set; }

        [JsonProperty(PropertyName = "brand")]
        public string Brand { get; set; }

        [JsonProperty(PropertyName = "occurrences")]
        public int Occurrences { get; set; }

        [JsonProperty(PropertyName = "source")]
        public RelationSource Source { get; set; }
    }

    internal class RelationSource
    {
        [JsonProperty(PropertyName = "url")]
        public string Url { get; set; }

        [JsonProperty(PropertyName = "text")]
        public string Text { get; set; }

        [JsonProperty(PropertyName = "date")]
        public DateTime Date { get; set; }
    }
}