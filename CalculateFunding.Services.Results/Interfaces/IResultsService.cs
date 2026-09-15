using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CalculateFunding.Models.Aggregations;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CalculateFunding.Services.Results.Interfaces
{
    public interface IResultsService : IJobProcessingService
    {
	    Task<IActionResult> GetProviderResults(string providerId, string specificationId);

        Task<IActionResult> GetProviderResultByCalculationType(string providerId, string specificationId, CalculationType calculationType);

        Task<IActionResult> GetProviderSpecifications(string providerId);

        Task<IActionResult> ProviderHasResultsBySpecificationId(string specificationId);

        Task<IActionResult> GetProviderResultsBySpecificationId(string specificationId, string top);

        Task<IActionResult> GetProviderSourceDatasetsByProviderIdAndSpecificationId(string specificationId, string providerId);

        Task<IActionResult> ReIndexCalculationProviderResults();

        Task<IActionResult> GetScopedProviderIdsBySpecificationId(string specificationId);

        Task<IActionResult> GetFundingCalculationResultsForSpecifications(SpecificationListModel specificationListModel);

        Task DeleteCalculationResults(ServiceBusReceivedMessage message);

        Task<IActionResult> HasCalculationResults(string calculationId);

        Task QueueCsvGenerationMessages();

        Task<IActionResult> QueueCsvGeneration(string specificationId);
        Task<IActionResult> GetSpecificationCalculationResultsMetadata(string specificationId);

        Task<IActionResult> GetAggregateCalculationResults(string specificationId, IEnumerable<string> calculationIds);
    }
}
