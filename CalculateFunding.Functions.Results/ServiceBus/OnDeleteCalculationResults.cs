using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Results.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Microsoft.Azure.Functions.Worker;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Results.ServiceBus
{
    public class OnDeleteCalculationResults : Retriable
    {
        private readonly IResultsService _resultsService;
        private const string FunctionName = "on-delete-calculation-results";
        private const string QueueName = ServiceBusConstants.QueueNames.DeleteCalculationResults;

        public OnDeleteCalculationResults(
            ILogger logger,
            IResultsService resultsService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, resultsService, refresherProvider)
        {
            Guard.ArgumentNotNull(resultsService, nameof(resultsService));

            _resultsService = resultsService;
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message, async () =>
            {
                await _resultsService.DeleteCalculationResults(message);
            });
        }
    }
}
