using CalculateFunding.Common.ApiClient.Policies.Models;
using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using CalculateFunding.Services.Publishing.Variations.Changes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.Variations.Strategies
{
    public class CalculationValuesUpdatedVariationStrategy : VariationStrategy, IVariationStrategy
    {
        public override string Name => "CalculationValuesUpdated";

        protected override Task<bool> Determine(ProviderVariationContext providerVariationContext, FundingConfiguration fundingConfiguration = null)
        {
            Guard.ArgumentNotNull(providerVariationContext, nameof(providerVariationContext));

            PublishedProviderVersion priorState = providerVariationContext?.PriorState;
            PublishedProviderVersion refreshState = providerVariationContext?.RefreshState;
            PublishedProviderVersion currentState = providerVariationContext?.CurrentState;

            if (priorState?.Calculations == null || refreshState?.Calculations == null)
            {
                return Task.FromResult(false);
            }

            foreach (FundingCalculation priorFundingCalculation in priorState.Calculations)
            {
                FundingCalculation refreshFundingCalculation = refreshState.Calculations.SingleOrDefault(_ => _.TemplateCalculationId == priorFundingCalculation.TemplateCalculationId);
                FundingCalculation currentFundingCalculation = currentState.Calculations.SingleOrDefault(_ => _.TemplateCalculationId == priorFundingCalculation.TemplateCalculationId);

                if (refreshFundingCalculation != null)
                {
                    bool priorNull = priorFundingCalculation?.Value == null;
                    bool refreshNull = refreshFundingCalculation?.Value == null;
                    bool currentMatch = currentFundingCalculation?.Value == refreshFundingCalculation?.Value;

                    if (priorNull != refreshNull 
                        || (priorFundingCalculation?.Value != refreshFundingCalculation?.Value) 
                        || (priorFundingCalculation?.Value == refreshFundingCalculation?.Value && !currentMatch))
                    {
                        return Task.FromResult(true);
                    }
                }
            }

            return Task.FromResult(false);
                               
        }

        protected override Task<bool> Execute(ProviderVariationContext providerVariationContext)
        {
            providerVariationContext.AddVariationReasons(VariationReason.CalculationValuesUpdated);

            providerVariationContext.QueueVariationChange(new MetaDataVariationsChange(providerVariationContext, Name));

            return Task.FromResult(false);
        }
    }
}
