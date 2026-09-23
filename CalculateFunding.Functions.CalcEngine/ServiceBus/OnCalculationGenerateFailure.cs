using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;

namespace CalculateFunding.Functions.CalcEngine.ServiceBus
{
    public class OnCalculationGenerateFailure : Failure
    {
        private const string QueueName = ServiceBusConstants.QueueNames.CalcEngineGenerateAllocationResultsPoisoned;

        public OnCalculationGenerateFailure(
            ILogger logger,
            IDeadletterService jobHelperService,
            IConfigurationRefresherProvider refresherProvider) : base (logger, jobHelperService, QueueName, refresherProvider)
        {
        }

        /// <summary>
        /// On poisoned message for running calcs.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="log"></param>
        [Function("on-calcs-generate-allocations-event-poisoned")]
        public async Task Run([ServiceBusTrigger(QueueName, Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message) 
            => await Process(message);
    }
}
