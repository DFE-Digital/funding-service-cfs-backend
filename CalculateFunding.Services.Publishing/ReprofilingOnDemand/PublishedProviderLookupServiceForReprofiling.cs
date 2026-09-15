using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Specifications.Models;

namespace CalculateFunding.Services.Publishing.ReprofilingOnDemand
{
    public class PublishedProviderLookupServiceForReprofiling : IPublishedProviderLookupServiceForReprofiling
    {
        private readonly IPublishedFundingBulkRepository _publishedFundingBulkRepository;

        public PublishedProviderLookupServiceForReprofiling(IPublishedFundingBulkRepository publishedFundingBulkRepository)
        {
            Guard.ArgumentNotNull(publishedFundingBulkRepository, nameof(publishedFundingBulkRepository));

            _publishedFundingBulkRepository = publishedFundingBulkRepository;
        }
        public async Task<IEnumerable<PublishedProvider>> GetPublishedProviderReprofilingSummaries(
            SpecificationSummary specificationSummary,
            IEnumerable<string> publishedProviderIds)
        {
            if (!publishedProviderIds.IsNullOrEmpty())
            {

                return await _publishedFundingBulkRepository.TryGetPublishedProvidersByProviderId(GetProviderIds(publishedProviderIds), specificationSummary.FundingStreams.First().Id, specificationSummary.FundingPeriod.Id);

            }

            return null;
        }
        private List<string> GetProviderIds(IEnumerable<string> publishedProviderIds)
        {
            List<string> providerIds = new List<string>();

            foreach (var id in publishedProviderIds)
            {
                // Split the string by '-' and get the last part to get the providerIds
                string[] parts = id.Split('-');
                string lastPart = parts[parts.Length - 1];

                providerIds.Add(lastPart);
            }

            return providerIds;
        }
    }
}
