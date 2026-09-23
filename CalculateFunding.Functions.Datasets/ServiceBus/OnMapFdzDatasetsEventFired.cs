using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Services.Processing.Functions;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Datasets.ServiceBus
{
    public class OnMapFdzDatasetsEventFired : Retriable
    {
        private readonly IProcessDatasetService _processDatasetService;

        public const string FunctionName = FunctionConstants.MapFdzDatasets;
        public const string QueueName = ServiceBusConstants.QueueNames.MapFdzDatasets;

        public OnMapFdzDatasetsEventFired(ILogger logger,
            IProcessDatasetService processDatasetService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, processDatasetService, refresherProvider)
        {
            Guard.ArgumentNotNull(processDatasetService, nameof(processDatasetService));

            _processDatasetService = processDatasetService;
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
            IsSessionsEnabled = true)] ServiceBusReceivedMessage message)
        {
            await base.Run(message, async () =>
            {
                await _processDatasetService.MapFdzDatasets(message);
            });
        }
    }
}
