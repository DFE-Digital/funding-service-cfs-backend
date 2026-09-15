using CalculateFunding.Common.ApiClient.Profiling.Models;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.Variations.Changes
{
    public class ConverterReProfileVariationChange : MidYearReProfileVariationChange
    {
        protected override string ChangeName => "Converter re-profile variation change";

        public ConverterReProfileVariationChange(ProviderVariationContext variationContext,
            string strategy, IEnumerable<string> indicativeToLiveFundingLines) : base(variationContext, strategy, indicativeToLiveFundingLines)
        {
        }

        protected override Task<(ReProfileRequest, bool)> BuildReProfileRequest(string fundingLineCode,
            PublishedProviderVersion refreshState,
            PublishedProviderVersion priorState,
            PublishedProviderVersion currentState,
            IApplyProviderVariations variationApplications,
            string profilePatternKey,
            ReProfileAudit reProfileAudit,
            FundingLine fundingLine,
            Func<string, string, ReProfileAudit, int, bool> reProfileForSameAmountFunc) =>
            variationApplications.ReProfilingRequestBuilder.BuildReProfileRequest(fundingLineCode,
                profilePatternKey,
                BuildCurrentPublishedProvider(currentState, refreshState, fundingLine),
                fundingLine.Value,
                reProfileAudit,
                midYearType: MidYearType.Converter,
                variationApplications.IsReProfileVariationAppliedOnDemand,
                priorState?.ReProfiledOnDemandProfiles != null ? priorState.ReProfiledOnDemandProfiles.Where(_ => _.FundingLineCode.Equals(fundingLineCode)).Any() : false,
                reProfileForSameAmountFunc: reProfileForSameAmountFunc);
    }
}
