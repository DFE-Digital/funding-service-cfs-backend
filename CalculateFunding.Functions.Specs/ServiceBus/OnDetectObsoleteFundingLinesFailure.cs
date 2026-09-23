using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;

namespace CalculateFunding.Functions.Specs.ServiceBus
{
    public class OnDetectObsoleteFundingLinesFailure : Failure
    {
        public const string FunctionName = "on-detect-obsolete-funding-lines-poisoned";
        private const string QueueName = ServiceBusConstants.QueueNames.DetectObsoleteFundingLinesPoisoned;

        public OnDetectObsoleteFundingLinesFailure(
            ILogger logger,
            IDeadletterService jobHelperService,
            IConfigurationRefresherProvider refresherProvider) : base (logger, jobHelperService, QueueName, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
                QueueName,
                Connection = ServiceBusConstants.ConnectionStringConfigurationKey)]
            ServiceBusReceivedMessage message) => await Process(message);
    }
}