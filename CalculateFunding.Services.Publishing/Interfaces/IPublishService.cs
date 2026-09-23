using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.Models;
using CalculateFunding.Services.Processing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.Interfaces
{
    public interface IPublishService : IJobProcessingService
    {
        Task PublishProviderFundingResults(ServiceBusReceivedMessage message, bool batched = false);
        Task PublishProviderFundingResults(bool batched, Reference author, string jobId, string correlationId, SpecificationSummary specification, PublishedProviderIdsRequest publishedProviderIdsRequest, bool enableIntegrityChecker);
    }
}
