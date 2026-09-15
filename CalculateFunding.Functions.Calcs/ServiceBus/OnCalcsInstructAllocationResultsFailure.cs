using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;

namespace CalculateFunding.Functions.Calcs.ServiceBus
{
    public class OnCalcsInstructAllocationResultsFailure : Failure
    {
        private const string FunctionName = "on-calcs-instruct-allocations-poisoned";
        private const string QueueName = ServiceBusConstants.QueueNames.CalculationJobInitialiserPoisoned;

        public OnCalcsInstructAllocationResultsFailure(
            ILogger logger,
            IDeadletterService jobHelperService,
            IConfigurationRefresherProvider refresherProvider) : base (logger, jobHelperService, QueueName, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(ServiceBusConstants.QueueNames.CalculationJobInitialiserPoisoned, Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message) 
            => await Process(message);
    }
}
