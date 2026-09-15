using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Results.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Microsoft.Azure.Functions.Worker;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Results.ServiceBus
{
    public class OnCalculationResultsCsvGeneration : Retriable
    {
        private const string FunctionName = "on-calculation-results-csv-generation";
        private const string QueueName = ServiceBusConstants.QueueNames.CalculationResultsCsvGeneration;

        public OnCalculationResultsCsvGeneration(
            ILogger logger,
            IProviderResultsCsvGeneratorService csvGeneratorService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, csvGeneratorService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
            IsSessionsEnabled = true)]
            ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}

