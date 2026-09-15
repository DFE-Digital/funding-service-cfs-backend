using CalculateFunding.Models.External.V4;
using CalculateFunding.Models.External;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using CalculateFunding.Api.External.V4.Models;

namespace CalculateFunding.Api.External.V4.Interfaces
{
    public interface IProviderFundingVersionService
    {
        Task<IActionResult> GetProviderFundingVersion(string channel, string providerFundingVersion);
        Task<IActionResult> GetFundings(string channel, string publishedProviderVersion);
        Task<ActionResult<SearchFeedResult<ExternalFeedFundingGroupItem>>> GetProviderNotificationFeedPage(
            HttpRequest request,
            HttpResponse response,
            int? pageRef,
            string channelUrlKey,
            int? ukprn,
            int? version,
            IEnumerable<string> fundingStreamIds,
            IEnumerable<string> fundingPeriodIds,          
            int? pageSize,
            System.Threading.CancellationToken cancellationToken);

        Task<ActionResult> GetFundingIdsByProviderFunding(
            HttpRequest request,
            HttpResponse response,          
            string channelUrlKey,
            IEnumerable<string> fundingStreamIds,
            IEnumerable<string> fundingPeriodIds,          
            System.Threading.CancellationToken cancellationToken);
    }
}
