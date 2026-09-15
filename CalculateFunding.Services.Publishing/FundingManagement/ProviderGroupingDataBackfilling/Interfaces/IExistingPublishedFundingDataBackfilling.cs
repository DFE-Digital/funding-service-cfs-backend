using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.FundingManagement.SqlModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling.Interfaces
{
    public interface IExistingPublishedFundingDataBackfilling
    {
        Task UpdateMissedProvidersInFundingGroup(List<PublishedProvider> commonProvidersInSameOrgGroup,
             SpecificationSummary specification, IEnumerable<Channel> allChannels, string selectedChannelCode);
    }
}
