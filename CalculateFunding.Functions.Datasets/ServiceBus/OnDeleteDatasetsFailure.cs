using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Datasets.ServiceBus
{
    public class OnDeleteDatasetsFailure : Failure
    {
        private const string FunctionName = "on-delete-datasets-poisoned";
        private const string QueueName = ServiceBusConstants.QueueNames.DeleteDatasetsPoisoned;

        public OnDeleteDatasetsFailure(
            ILogger logger,
            IDeadletterService jobHelperService,
            IConfigurationRefresherProvider refresherProvider) : base(logger, jobHelperService, QueueName, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(QueueName, Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await Process(message);
        }
    }
}
