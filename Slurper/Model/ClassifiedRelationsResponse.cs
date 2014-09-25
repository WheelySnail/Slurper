namespace Slurper.Model
{
    #region Using Directives

    using System.Collections.Generic;

    using Newtonsoft.Json;

    #endregion

    internal class ClassifiedRelationsResponse
    {
        [JsonProperty(PropertyName = "relations")]
        public List<ClassifiedRelation> Relations { get; set; }
    }
}