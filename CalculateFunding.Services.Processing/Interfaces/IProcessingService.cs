using Azure.Messaging.ServiceBus;
using System;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Processing.Interfaces
{
    public interface IProcessingService
    {
        Task Run(ServiceBusReceivedMessage message, Func<Task> func = null);

        Task Process(ServiceBusReceivedMessage message);
    }
}
