using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Services.Processing.Interfaces
{
    public interface IDeadletterService
    {
        Task Process(ServiceBusReceivedMessage message);
    }
}
