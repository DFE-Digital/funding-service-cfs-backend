using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.CalcEngine.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CalculateFunding.Runners.CalcEngine
{
    public class OnCalcsGenerateAllocationResults : ServiceBusQueueWorker<ServiceBusReceivedMessage>
    {
        private readonly ICalculationEngineService _calculationEngineService;

        public OnCalcsGenerateAllocationResults(IConfiguration configuration,
            ICalculationEngineService calculationEngineService, IHostApplicationLifetime hostApplicationLifetime, ILogger<OnCalcsGenerateAllocationResults> logger)
            : base(configuration, hostApplicationLifetime, logger)
        {
            _calculationEngineService = calculationEngineService;
        }

        protected override async Task ProcessMessage(ServiceBusReceivedMessage message, string messageId, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Processing message {message}", message);

            await _calculationEngineService.Process(message);

            Logger.LogInformation("Message {message} processed", message);
        }
    }
}