using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.FundingManagement.SqlModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.FundingManagement.Interfaces
{
    public interface IPublishedFundingContentsChannelPersistenceService
    {
        Task SavePublishedFundingContents(
            IEnumerable<PublishedFundingVersion> publishedFundingVersionsToSave,
            Channel channel);

        Task UploadBlobAsync(string blobName, string content, string specificationId);

        Task<bool> IsBlobExists(string blobName);
    }
}
