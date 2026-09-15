using Newtonsoft.Json;
using System;

namespace CalculateFunding.Models.Publishing.Reprofiling
{
    public class ProviderSummaryResult
    {
        [JsonProperty("UKPRN")]
        public string UKPRN;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("openDate")]
        public DateTimeOffset? OpenDate;

        [JsonProperty("isEligible")]
        public bool IsEligible;

        [JsonProperty("errorMessage")]
        public string ErrorMessage;
    }
}