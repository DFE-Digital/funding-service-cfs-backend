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
    public class ConverterReProfilingVariationStrategy : VariationStrategy, IVariationStrategy
    {
        public override string Name => "ConverterReProfiling";

        protected override Task<bool> Determine(ProviderVariationContext providerVariationContext, FundingConfiguration fundingConfiguration = null)
        {
            Guard.ArgumentNotNull(providerVariationContext, nameof(providerVariationContext));

            PublishedProviderVersion priorState = providerVariationContext.PriorState;
            PublishedProviderVersion refreshState = providerVariationContext.RefreshState;

            if (providerVariationContext.UpdatedProvider.Status == Closed ||
                VariationPointersNotSet(providerVariationContext) ||
                !refreshState.IsConverter(providerVariationContext.FundingPeriodStartDate,
                    providerVariationContext.FundingPeriodEndDate,
                    AcademyConverter,
                    providerVariationContext.IsReProfileVariationAppliedOnDemand))
            {
                return Task.FromResult(false);
            }

            IEnumerable<string> inYearConverterFundingLines = InYearConverterFundingLines(refreshState,
                priorState);

            IEnumerable<string> indicativeAffectedFundingLines = providerVariationContext.AffectedFundingLineCodes("IndicativeToLive");

            //US 157447 - If the provider is not indicative to live when applying Reprofile strategy on Demand,
            //applying rebrokerage scenario for a new provider
            if (providerVariationContext.IsReProfileVariationAppliedOnDemand && indicativeAffectedFundingLines.IsNullOrEmpty())
            {
                inYearConverterFundingLines = InYearConverterFundingLinesToReProfileOnDemand(refreshState);
            }

            if (inYearConverterFundingLines.IsNullOrEmpty() && indicativeAffectedFundingLines.IsNullOrEmpty())
            {
                return Task.FromResult(false);
            }

            Concatenate(inYearConverterFundingLines ?? ArraySegment<string>.Empty,
                        indicativeAffectedFundingLines ?? ArraySegment<string>.Empty)
                        .ForEach(_ => providerVariationContext.AddAffectedFundingLineCode(Name, _));

            return Task.FromResult(true);
        }

        private static IEnumerable<T> Concatenate<T>(params IEnumerable<T>[] lists)
        {
            return lists.SelectMany(_ => _);
        }

        private IEnumerable<string> InYearConverterFundingLines(PublishedProviderVersion refreshState,
            PublishedProviderVersion priorState)
        {
            List<string> fundingLines = new List<string>();

            if(priorState!=null)
            {
                return null;
            }
            
            // we only need to re-profile an opener if it has a none zero value
            foreach (FundingLine fundingLine in refreshState.PaymentFundingLinesWithValues.Where(_ => _.Value != 0))
            {
                fundingLines.Add(fundingLine.FundingLineCode);
            }

            return fundingLines;
        }
        
        private IEnumerable<string> InYearConverterFundingLinesToReProfileOnDemand(PublishedProviderVersion refreshState)
        {
            List<string> fundingLines = new List<string>();

            // we only need to re-profile an opener if it has a none zero value
            foreach (FundingLine fundingLine in refreshState.PaymentFundingLinesWithValues.Where(_ => _.Value != 0))
            {
                fundingLines.Add(fundingLine.FundingLineCode);
            }

            return fundingLines;
        }

        private static bool VariationPointersNotSet(ProviderVariationContext providerVariationContext) => providerVariationContext.VariationPointers.IsNullOrEmpty();

        protected override Task<bool> Execute(ProviderVariationContext providerVariationContext)
        {
            providerVariationContext.QueueVariationChange(new ConverterReProfileVariationChange(providerVariationContext, Name, providerVariationContext.AffectedFundingLineCodes("IndicativeToLive")));

            return Task.FromResult(true);
        }
    }
}
