using System.Collections.Generic;

namespace CalculateFunding.Api.External.V4.Models
{
    public class FundingFeedId
    {
        public string ProviderFundingId { get; set; }
        public IEnumerable<string> FundingIds { get; set; }
    }
}
