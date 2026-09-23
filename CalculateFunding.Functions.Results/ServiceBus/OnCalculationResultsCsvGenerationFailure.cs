using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Microsoft.Azure.Functions.Worker;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Results.ServiceBus
{
    public class OnCalculationResultsCsvGenerationFailure : Failure
    {
        public const string FunctionName = "on-calculation-results-csv-generation-poisoned";
        private const string QueueName = ServiceBusConstants.QueueNames.CalculationResultsCsvGenerationPoisoned;

        public OnCalculationResultsCsvGenerationFailure(
            ILogger logger,
            IDeadletterService jobHelperService,
            IConfigurationRefresherProvider refresherProvider) : base (logger, jobHelperService, QueueName, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
                QueueName,
                Connection = ServiceBusConstants.ConnectionStringConfigurationKey)]
            ServiceBusReceivedMessage message)
        {
            await Process(message);
        }
    }
}