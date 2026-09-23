using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Policies.Models;
using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using CalculateFunding.Services.Publishing.Variations.Changes;

namespace CalculateFunding.Services.Publishing.Variations.Strategies
{
    public class ProfilingUpdatedVariationStrategy : ProfilingChangeVariation, IVariationStrategy
    {
        public override string Name => "ProfilingUpdated";

        protected override Task<bool> Determine(ProviderVariationContext providerVariationContext, FundingConfiguration fundingConfiguration = null)
        {
            Guard.ArgumentNotNull(providerVariationContext, nameof(providerVariationContext));

            PublishedProviderVersion priorState = providerVariationContext.PriorState;

            //If Provider is already released with close status  then we need to add the variation reason
            if (priorState != null &&
               providerVariationContext.ReleasedState != null &&
               priorState.Provider.Status == Closed &&
               providerVariationContext.UpdatedProvider.Status == Closed)
            {
                return Task.FromResult(true);
            }

            //If Provider is new then we need to add the variation reason
            if (priorState == null &&
               providerVariationContext.ReleasedState != null &&              
               providerVariationContext.UpdatedProvider.Status == Closed)
            {
                return Task.FromResult(true);
            }

            if (priorState == null ||
                providerVariationContext.ReleasedState == null ||
                priorState.Provider.Status == Closed ||
                providerVariationContext.UpdatedProvider.Status == Closed )
            {
                return Task.FromResult(false);
            }

            IEnumerable<string> fundingLinesWithProfilingChanges = FundingLinesWithProfilingChanges(priorState, providerVariationContext.RefreshState);

            if (fundingLinesWithProfilingChanges.IsNullOrEmpty() && !providerVariationContext.IsReProfileVariationAppliedOnDemand)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        protected override Task<bool> Execute(ProviderVariationContext providerVariationContext)
        {
            providerVariationContext.AddVariationReasons(VariationReason.ProfilingUpdated);

            providerVariationContext.QueueVariationChange(new MetaDataVariationsChange(providerVariationContext, Name));

            return Task.FromResult(false);
        }
    }
}