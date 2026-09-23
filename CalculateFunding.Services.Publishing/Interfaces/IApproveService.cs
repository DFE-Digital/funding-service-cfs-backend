using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Processing.Interfaces;

namespace CalculateFunding.Services.Publishing.Interfaces
{
    public interface IApproveService : IJobProcessingService
    {
        Task ApproveResults(ServiceBusReceivedMessage message, bool batched = false);
    }
}
