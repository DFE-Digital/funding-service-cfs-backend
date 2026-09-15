using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Policies.Models;
using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Services.Publishing.Models;

namespace CalculateFunding.Services.Publishing.Interfaces
{
    public interface IVariationStrategy
    {
        string Name { get; }
        
        Task<bool> Process(ProviderVariationContext providerVariationContext, FundingConfiguration fundingConfiguration = null);
    }
}