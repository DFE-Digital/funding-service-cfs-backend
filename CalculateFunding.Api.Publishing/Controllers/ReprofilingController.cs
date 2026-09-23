using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Publishing.Reprofiling;
using CalculateFunding.Services.Publishing.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Common.ApiClient.Publishing.Models;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Publishing.Models;
using ApiJob = CalculateFunding.Common.ApiClient.Jobs.Models.Job;
using CalculateFunding.Common.ApiClient.Jobs.Models;
using PublishedProviderIdsRequest = CalculateFunding.Services.Publishing.Models.PublishedProviderIdsRequest;
using CalculateFunding.Common.Models;

namespace CalculateFunding.Api.Publishing.Controllers
{
    [ApiController]
    public class ReprofilingController : Controller
    {
        private readonly IReprofilingStatusService _reprofilingStatusService;

        public ReprofilingController(IReprofilingStatusService reprofilingStatusService)
        {
            Guard.ArgumentNotNull(reprofilingStatusService, nameof(reprofilingStatusService));

            _reprofilingStatusService = reprofilingStatusService;
        }

        /// <summary>
        ///     Get the reprofiling on demand summary data
        /// </summary>
        /// <param name="providerIds">the provider ids making up the batch</param>
        /// <param name="specificationId">the specification id to limit the published provider ids to</param>
        /// <returns>ReprofilingSummaryResult</returns>
        [HttpPost("api/specifications/{specificationId}/reprofiling/providerstatus-for-reprofilingondemand")]
        [ProducesResponseType(200, Type = typeof(ReprofilingSummaryResult))]
        public async Task<IActionResult> GetProviderBatchForReprofilingOnDemand(
            [FromBody] PublishedProviderIdsRequest providerIds,
            [FromRoute] string specificationId) =>
            await _reprofilingStatusService.GetProviderBatchResultForReprofiling(providerIds, specificationId);


        ///// <summary>
        /////     Get the reprofiling on demand summary data
        ///// </summary>
        ///// <param name="providerIds">the provider ids making up the batch</param>
        ///// <param name="specificationId">the specification id to limit the published provider ids to</param>
        ///// <returns>ReprofilingSummaryResult</returns>
        [HttpPost("api/specifications/{specificationId}/reprofilingondemand")]
        [ProducesResponseType(200, Type = typeof(Job))]
        public async Task<IActionResult> ReProfilingOnDemand(
            [FromRoute] string specificationId, [FromBody] PublishedProviderIdsRequest publishedProviderIdsRequest)
        {
            Reference user = ControllerContext.HttpContext.Request.GetUserOrDefault();
            string correlationId = ControllerContext.HttpContext.Request.GetCorrelationId();

            return await _reprofilingStatusService.QueueReprofilingOnDemand(specificationId, publishedProviderIdsRequest, user, correlationId);
        }

    }
}
