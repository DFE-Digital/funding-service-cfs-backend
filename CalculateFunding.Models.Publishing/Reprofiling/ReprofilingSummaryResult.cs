using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculateFunding.Models.Publishing.Reprofiling
{
    public class ReprofilingSummaryResult
    {
        [JsonProperty("totalProviders")]
        public int TotalProviders;

        [JsonProperty("totalEligibleProviders")]
        public int TotalEligibleProviders;

        [JsonProperty("providerSummaryResult")]
        public List<ProviderSummaryResult> ProviderSummaryResult;

        [JsonProperty("url")]
        public string Url { get; set; }

    }
}
