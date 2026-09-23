using CalculateFunding.Api.External.V4.Interfaces;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.External.V4;
using CalculateFunding.Models.External;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CalculateFunding.Models.External.AtomItems;
using CalculateFunding.Api.External.V4.Models;

namespace CalculateFunding.Api.External.V4.Controllers
{
    [Authorize(Roles = Constants.ExecuteApiRole)]
    [ApiController]
    [ApiVersion("4.0")]
    [Route("api/v{version:apiVersion}/{channel}/funding/provider")]
    public class ProviderFundingVersionController : ControllerBase
    {
        private readonly IProviderFundingVersionService _providerFundingVersionService;

        public ProviderFundingVersionController(IProviderFundingVersionService providerFundingVersionService)
        {
            Guard.ArgumentNotNull(providerFundingVersionService, nameof(providerFundingVersionService));

            _providerFundingVersionService = providerFundingVersionService;
        }

        /// <summary>
        /// Gets provider funding version based on key from notification feed
        /// </summary>
        /// <param name="channel">Channel</param>
        /// <param name="providerFundingVersion">Provider Funding Version Key</param>
        /// <returns>Provider Version contents</returns>
        [HttpGet("{providerFundingVersion}")]
        [Produces(typeof(object))]
        public async Task<IActionResult> GetFunding(
            [FromRoute] string channel,
            [FromRoute] string providerFundingVersion)
        {
            return await _providerFundingVersionService.GetProviderFundingVersion(channel, providerFundingVersion);
        }

        /// <summary>
        /// Gets provider funding based on prublished funding version
        /// </summary>
        /// <param name="channel">Channel</param>
        /// <param name="publishedProviderVersion">Published Provider Version</param>
        /// <returns>Provider Version contents</returns>
        [HttpGet("{publishedProviderVersion}/fundings")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<dynamic>))]
        public async Task<IActionResult> GetFundings(
            [FromRoute] string channel,
            [FromRoute] string publishedProviderVersion)
        {
            return await _providerFundingVersionService.GetFundings(channel, publishedProviderVersion);
        }

        /// <summary>
        /// Funding feed - initial page with latest results
        /// </summary>
        /// <param name="channel">Release purpose channel</param>
        /// <param name="fundingStreamIds">Optional Funding stream IDs</param>
        /// <param name="fundingPeriodIds">Optional Funding Period IDs</param>
        /// <param name="pageSize">Page Size</param>
        /// <param name="ukprn">Optional ukprn</param>
        /// <param name="version">Optional version</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Feed of funding notifications based on query parameters</returns>
        [HttpGet("notifications")]
        [Produces(typeof(AtomFeed<object>))]
        public async Task<ActionResult<SearchFeedResult<ExternalFeedFundingGroupItem>>> GetProviderVersions(
            [FromRoute] string channel,
            [FromQuery] int? ukprn,
            [FromQuery] int? version,
            [FromQuery] string[] fundingStreamIds,
            [FromQuery] string[] fundingPeriodIds,         
            [FromQuery] int? pageSize,          
            CancellationToken cancellationToken
            )
        {
            return await _providerFundingVersionService.GetProviderNotificationFeedPage(Request, Response, null, channel, ukprn, version, fundingStreamIds,
                 fundingPeriodIds, pageSize, cancellationToken);
        }


        /// <summary>
        /// Funding feed - specific historical page of results.
        /// When requested with a specific page size, the items returned in the page will be the same.
        /// Page numbers with lower values container older items.
        /// </summary>
        /// <param name="channel">Release purpose channel</param>
        /// <param name="fundingStreamIds">Optional Funding stream IDs</param>
        /// <param name="fundingPeriodIds">Optional Funding Period IDs</param>     
        /// <param name="pageSize">Page Size. Maximum size of 500</param>
        /// <param name="pageRef">Page reference for historical page</param>
        /// <param name="ukprn">Optional ukprn</param>
        /// <param name="version">Optional version</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("notifications/{pageRef:int}")]
        [Produces(typeof(AtomFeed<object>))]
        public async Task<ActionResult<SearchFeedResult<ExternalFeedFundingGroupItem>>> GetProviderVersionsPage(
            [FromRoute] string channel,
            [FromQuery] int? ukprn,
            [FromQuery] int? version,
            [FromQuery] string[] fundingStreamIds,
            [FromQuery] string[] fundingPeriodIds,          
            [FromQuery] int? pageSize,
            [FromRoute] int pageRef,            
            CancellationToken cancellationToken)
        {
            return await _providerFundingVersionService.GetProviderNotificationFeedPage(Request, Response, pageRef, channel, ukprn, version,fundingStreamIds,
                 fundingPeriodIds, pageSize ,cancellationToken);
        }

        /// <summary>
        /// Funding feed - initial page with latest results
        /// </summary>
        /// <param name="channel">Release purpose channel</param>
        /// <param name="fundingStreamIds">Optional Funding stream IDs</param>
        /// <param name="fundingPeriodIds">Optional Funding Period IDs</param>      
        /// <returns>Get fundingIds based on ProviderFundingId</returns>
        [HttpGet("fundingIds")]
        [Produces(typeof(AtomFeed<object>))]
        public async Task<ActionResult> GetFundingIdsByProviderFunding(
            [FromRoute] string channel,
            [FromQuery] string[] fundingStreamIds,
            [FromQuery] string[] fundingPeriodIds,
            CancellationToken cancellationToken
            )
        {
            return await _providerFundingVersionService.GetFundingIdsByProviderFunding(Request, Response, channel, fundingStreamIds,
                 fundingPeriodIds, cancellationToken);
        }      
    }
}
