using CalculateFunding.Common.ApiClient.Profiling.Models;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.Variations.Changes
{
    public class ClosedProviderReProfileVariationChange : ReProfileVariationChange
    {
        protected override string ChangeName => "Closed provider re-profile variation change";

        public ClosedProviderReProfileVariationChange(ProviderVariationContext variationContext, string strategy) : base(variationContext, strategy)
        {
        }

        protected override PublishedProviderVersion GetState(PublishedProviderVersion currentState, PublishedProviderVersion priorState, bool sameAsAmount)
        {
            if (sameAsAmount)
            {
                // if amount hasn't changed then for closure re-profiling we need to copy the released state to the refresh state
                // as we use the last released state to calculate the mid-year closure funding
                return priorState;
            }

            return currentState;
        }

        protected override async Task<(ReProfileRequest request, bool shouldExecuteForSameAsKey)> BuildReProfileRequest(string fundingLineCode,
            PublishedProviderVersion refreshState,
            PublishedProviderVersion priorState,
            PublishedProviderVersion currentState,
            IApplyProviderVariations variationApplications,
            string profilePatternKey,
            ReProfileAudit reProfileAudit,
            FundingLine fundingLine,
            Func<string, string, ReProfileAudit, int, bool> reProfileForSameAmountFunc)
        {
            (ReProfileRequest reProfileRequest, bool shouldExecuteForSameAsKey) = await variationApplications.ReProfilingRequestBuilder.BuildReProfileRequest(fundingLineCode,
                profilePatternKey,
                priorState,
                fundingLine.Value,
                reProfileAudit,
                MidYearType.Closure,
                isReProfileOnDemandTriggered: variationApplications.IsReProfileVariationAppliedOnDemand,
                hasPreviouslyReProfiledOnDemand: priorState?.ReProfiledOnDemandProfiles != null ? priorState.ReProfiledOnDemandProfiles.Where(_ => _.FundingLineCode.Equals(fundingLineCode)).Any() : false,
                reProfileForSameAmountFunc: reProfileForSameAmountFunc);

            return (reProfileRequest, shouldExecuteForSameAsKey);
        }
    }
}
