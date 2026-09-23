using CalculateFunding.Common.Models;
using CalculateFunding.Services.Publishing.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.Interfaces
{
    public interface IReprofilingStatusService
    {
        Task<IActionResult> GetProviderBatchResultForReprofiling(PublishedProviderIdsRequest providerIds, string specificationId);

        Task<IActionResult> QueueReprofilingOnDemand(string specificationId, PublishedProviderIdsRequest publishedProviderIdsRequest, Reference author, string correlationId);
    }
}
