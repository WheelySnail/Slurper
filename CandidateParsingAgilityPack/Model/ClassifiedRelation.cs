﻿namespace CandidateParsingAgilityPack.Model
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

        [JsonProperty(PropertyName = "brands")]
        public List<string> Brands { get; set; }

        [JsonProperty(PropertyName = "company")]
        public string Company { get; set; }

        [JsonProperty(PropertyName = "source")]
        public RelationSource Source { get; set; }

        [JsonProperty(PropertyName = "brand")]
        public string Brand { get; set; }
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