using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Processing.Interfaces;
using System;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Processing
{
    public abstract class ProcessingService : IProcessingService
    {
        public virtual async Task Run(ServiceBusReceivedMessage message, Func<Task> func = null)
        {
            if (func != null)
            {
                await func();
            }
            else
            {
                await Process(message);
            }
        }

        public abstract Task Process(ServiceBusReceivedMessage message);
    }
}
