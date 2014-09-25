namespace Slurper.Model
{
    #region Using Directives

    using Newtonsoft.Json;

    #endregion

    internal class FreebaseBusinessOperation
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }
}