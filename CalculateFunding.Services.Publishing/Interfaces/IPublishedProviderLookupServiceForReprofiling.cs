using CalculateFunding.Models.Publishing.Reprofiling;
using CalculateFunding.Models.Publishing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Specifications.Models;

namespace CalculateFunding.Services.Publishing.Interfaces
{
    public interface IPublishedProviderLookupServiceForReprofiling
    {
        Task<IEnumerable<PublishedProvider>> GetPublishedProviderReprofilingSummaries(
            SpecificationSummary specificationSummary,
            IEnumerable<string> publishedProviderIds);
    }
}
