using Azure.Messaging.ServiceBus;
using Serilog;

namespace CalculateFunding.Services.CalcEngine.Interfaces
{
    public interface ICalculationEngineServiceValidator
    {
        void ValidateMessage(ILogger logger, ServiceBusReceivedMessage message);
    }
}
