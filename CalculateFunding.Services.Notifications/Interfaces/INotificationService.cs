using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Azure.Functions.Worker;

namespace CalculateFunding.Services.Notifications.Interfaces
{
    public interface INotificationService : IProcessingService
    {
        Task<IEnumerable<SignalRMessageAction>> OnNotificationEvent(ServiceBusReceivedMessage message);
    }
}
