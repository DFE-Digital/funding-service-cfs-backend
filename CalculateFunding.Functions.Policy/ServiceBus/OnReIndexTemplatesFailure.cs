using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Microsoft.Azure.Functions.Worker;

namespace CalculateFunding.Functions.Policy.ServiceBus
{
    public class OnReIndexTemplatesFailure : Failure
    {
        private const string FunctionName = "on-dataset-validation-events-poisoned";
        private const string QueueName = ServiceBusConstants.QueueNames.PolicyReIndexTemplatesPoisoned;

        public OnReIndexTemplatesFailure(
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
