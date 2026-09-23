using Newtonsoft.Json;

namespace CalculateFunding.Models.Datasets
{
    public class DatasetComparisonField
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
