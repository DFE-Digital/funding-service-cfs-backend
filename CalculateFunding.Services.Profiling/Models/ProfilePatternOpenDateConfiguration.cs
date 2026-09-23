using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CalculateFunding.Services.Profiling.Models
{
    public class ProfilePatternOpenDateConfiguration
    {
        [JsonProperty("openDateStart")]
        public DateTime OpenDateStart { get; set; }

        [JsonProperty("openDateEnd")]
        public DateTime OpenDateEnd { get; set; }

        [JsonProperty("providerType")]
        public string ProviderType { get; set; }

        [JsonProperty("openReason")]
        public IEnumerable<string> OpenReason { get; set; }
    }
}
