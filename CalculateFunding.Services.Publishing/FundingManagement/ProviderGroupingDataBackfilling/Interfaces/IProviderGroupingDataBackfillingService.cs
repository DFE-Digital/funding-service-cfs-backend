using CalculateFunding.Common.Models;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling.Interfaces
{
    public interface IProviderGroupingDataBackfillingService : IProcessingService
    {
        Task<IActionResult> QueueProviderGroupingDataBackfillingJob(Reference author,
                string correlationId,
                string specificationId, string channelCode, string statusChangedDate);
    }
}
