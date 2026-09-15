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
    public class OnDeleteDatasets : Retriable
    {
        private readonly ILogger _logger;
        private readonly IDatasetService _datasetService;
        public const string FunctionName = "on-delete-datasets";
        private const string QueueName = ServiceBusConstants.QueueNames.DeleteDatasets;

        public OnDeleteDatasets(
            ILogger logger,
            IDatasetService datasetService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, datasetService, refresherProvider)
        {
            Guard.ArgumentNotNull(datasetService, nameof(datasetService));

            _datasetService = datasetService;
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message, async () =>
            {
                await _datasetService.DeleteDatasets(message);
            });
        }
    }
}
