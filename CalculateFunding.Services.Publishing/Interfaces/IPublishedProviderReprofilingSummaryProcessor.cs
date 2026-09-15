using CalculateFunding.Common.ApiClient.Policies.Models;
using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Models.Publishing.Reprofiling;
using CalculateFunding.Services.Publishing.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.Interfaces
{
    public interface IPublishedProviderReprofilingSummaryProcessor
    {
        Task<ReprofilingSummaryResult> GetFundingSummaryForReprofilingPublishedProviders(IEnumerable<string> publishedProviderIds,
            SpecificationSummary specificationSummary,
            FundingConfiguration fundingConfiguration,
            FundingPeriod fundingPeriod,
            IEnumerable<ProfileVariationPointer> currentProfileVariationPointerResult);
    }
}
