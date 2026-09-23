using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using CalculateFunding.Services.Publishing.Variations.Changes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.Variations.Strategies
{
    public class ClosedProviderReProfilingVariationStrategy : ProfilingChangeVariation, IVariationStrategy
    {

        public override string Name => "ClosedProviderReProfiling";

        protected override Task<bool> Determine(ProviderVariationContext providerVariationContext, FundingConfiguration fundingConfiguration = null)
        {
            Guard.ArgumentNotNull(providerVariationContext, nameof(providerVariationContext));

            PublishedProviderVersion priorState = providerVariationContext.PriorState;
            PublishedProviderVersion refreshState = providerVariationContext.RefreshState;

            if (priorState == null ||
                providerVariationContext.ReleasedState == null ||
                priorState.Provider.Status != Closed ||
                providerVariationContext.UpdatedProvider.Status != Closed)
            {
                return Task.FromResult(false);
            }

            IEnumerable<string> fundingLinesWithProfilingChanges = FundingLinesWithProfilingChanges(priorState, refreshState);

            if (fundingLinesWithProfilingChanges.IsNullOrEmpty() ||
                HasNoPaidPeriods(providerVariationContext, priorState))
            {
                return Task.FromResult(false);
            }

            fundingLinesWithProfilingChanges.ForEach(_ => providerVariationContext.AddAffectedFundingLineCode(Name, _));

            return Task.FromResult(true);
        }

        protected override bool ExtraFundingLinePredicate(PublishedProviderVersion refreshState,
            FundingLine fundingLine)
            => !refreshState.FundingLineHasCustomProfile(fundingLine.FundingLineCode);

        protected override IEnumerable<string> FundingLinesWithProfilingChanges(PublishedProviderVersion priorState,
            PublishedProviderVersion refreshState) =>
            Enumerable.Concat(base.FundingLinesWithProfilingChanges(priorState, refreshState),
            FundingLinesWithCarryOverChanges(priorState, refreshState));

        protected override Task<bool> Execute(ProviderVariationContext providerVariationContext)
        {
            providerVariationContext.QueueVariationChange(new ClosedProviderReProfileVariationChange(providerVariationContext, Name));

            return Task.FromResult(false);
        }

        private IEnumerable<string> FundingLinesWithCarryOverChanges(PublishedProviderVersion priorState,
            PublishedProviderVersion refreshState)
        {
            List<string> fundingLines = new List<string>();

            foreach (ProfilingCarryOver carryOver in priorState.CarryOvers ?? ArraySegment<ProfilingCarryOver>.Empty)
            {
                ProfilingCarryOver latestCustomProfile = refreshState.CarryOvers?.SingleOrDefault(_ => _.FundingLineCode == carryOver.FundingLineCode);

                if ((latestCustomProfile?.Amount).GetValueOrDefault() != carryOver.Amount)
                {
                    fundingLines.Add(carryOver.FundingLineCode);
                }
            }

            return fundingLines;
        }
    }
}
