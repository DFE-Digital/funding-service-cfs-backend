using CalculateFunding.Common.Models;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Publishing.FundingManagement;
using CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CalculateFunding.Api.Publishing.Controllers
{
    [ApiController]
    public class ReleaseManagementMigrationController : ControllerBase
    {
        private readonly IPublishingV3ToSqlMigrator _migrator;
        private readonly IProviderGroupingDataBackfillingService _providerGroupDataBackfillingService;

        public ReleaseManagementMigrationController(IPublishingV3ToSqlMigrator publishingV3ToSqlMigrator,
            IProviderGroupingDataBackfillingService providerGroupDataBackfillingService)
        {
            _migrator = publishingV3ToSqlMigrator;
            _providerGroupDataBackfillingService = providerGroupDataBackfillingService;
        }

        [HttpGet("api/releasemanagement/queuereleasemanagementdatamigrationjob")]
        public async Task<IActionResult> QueueReleaseManagementDataMigrationJob([FromQuery] string[] fundingStreamIds, [FromQuery] string fundingPeriodId)
        {
            Reference user = ControllerContext.HttpContext.Request.GetUserOrDefault();
            string correlationId = ControllerContext.HttpContext.Request.GetCorrelationId();
            
            return await _migrator.QueueReleaseManagementDataMigrationJob(user, correlationId, fundingStreamIds, fundingPeriodId);
        }
        /// <summary>
        /// This API will trigger the job to backfill the provider data in to published funding, 
        /// which is being missed in the provious release
        /// </summary>
        /// <param name="specificationId"></param>
        /// <param name="channelCode">The channel code in which the wrong release has happened</param>
        /// <param name="statusChangedDate">Any date which should be in between the release with wrong configuration and and the release with correct release</param>
        /// <returns></returns>
        [HttpGet("api/releasemanagement/queueprovidergroupdatabackfillingjob")]
        public async Task<IActionResult> QueueProviderGroupDataBackfillingJob([FromQuery] string specificationId, [FromQuery] string channelCode, [FromQuery] string statusChangedDate)
        {
            Reference user = ControllerContext.HttpContext.Request.GetUserOrDefault();
            string correlationId = ControllerContext.HttpContext.Request.GetCorrelationId();

            return await _providerGroupDataBackfillingService.QueueProviderGroupingDataBackfillingJob(user, correlationId, specificationId, channelCode, statusChangedDate);
        }
    }
}
