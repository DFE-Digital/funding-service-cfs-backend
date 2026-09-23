using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;

namespace CalculateFunding.Functions.Calcs.ServiceBus
{
    public class OnUpdateCodeContextCacheFailure : Failure
    {
        private const string FunctionName = "on-update-code-context-cache-poisoned";
        private const string QueueName = ServiceBusConstants.QueueNames.UpdateCodeContextCachePoisoned;

        public OnUpdateCodeContextCacheFailure(
            ILogger logger,
            IDeadletterService jobHelperService,
            IConfigurationRefresherProvider refresherProvider) : base (logger, jobHelperService, QueueName, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
                ServiceBusConstants.QueueNames.UpdateCodeContextCachePoisoned,
                Connection = ServiceBusConstants.ConnectionStringConfigurationKey)]
            ServiceBusReceivedMessage message) => await Process(message);
    }
}