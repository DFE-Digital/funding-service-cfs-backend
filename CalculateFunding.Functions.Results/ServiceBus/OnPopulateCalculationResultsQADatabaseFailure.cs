using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Microsoft.Azure.Functions.Worker;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Results.ServiceBus
{
    public class OnPopulateCalculationResultsQADatabaseFailure : Failure
    {
        private const string FunctionName = FunctionConstants.PopulateCalculationResultsQaDatabasePoisoned;
        private const string QueueName = ServiceBusConstants.QueueNames.PopulateCalculationResultsQADatabasePoisoned;

        public OnPopulateCalculationResultsQADatabaseFailure(
            ILogger logger,
            IDeadletterService jobHelperService,
            IConfigurationRefresherProvider refresherProvider) : base(logger, jobHelperService, QueueName, refresherProvider)
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
