using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CalculateFunding.Services.Datasets.Interfaces
{
    public interface IProcessDatasetService : IJobProcessingService
    {
        Task<IActionResult> GetDatasetAggregationsBySpecificationId(string specificationId);

        Task MapFdzDatasets(ServiceBusReceivedMessage message);
    }
}
