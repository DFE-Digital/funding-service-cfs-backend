using System;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Profiling.Models;
using CalculateFunding.Models.Publishing;

namespace CalculateFunding.Services.Publishing.Interfaces
{
    public interface IReProfilingRequestBuilder
    {
        Task<(ReProfileRequest request, bool shouldExecuteForSameAsKey)> BuildReProfileRequest(string fundingLineCode,
            string profilePatternKey,
            PublishedProviderVersion publishedProviderVersion,
            decimal? fundingLineTotal = null,
            ReProfileAudit reProfileAudit = null,
            MidYearType? midYearType = null,
            bool isReProfileOnDemandTriggered = false,
            bool hasPreviouslyReProfiledOnDemand = false,
            Func<string, string, ReProfileAudit, int, bool> reProfileForSameAmountFunc = null);
    }
}