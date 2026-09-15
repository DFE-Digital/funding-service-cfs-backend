using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Services.Processing.Functions;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Datasets.ServiceBus
{
    public class OnRunConverterDataMerge : Retriable
    {
        public const string FunctionName = "on-run-converter-data-merge";
        private const string DatasetsConverterDatasetMerge = ServiceBusConstants.QueueNames.RunConverterDatasetMerge;

        public OnRunConverterDataMerge(
            ILogger logger,
            IConverterDataMergeService converterDataMergeService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger,
                messengerService,
                FunctionName,
                DatasetsConverterDatasetMerge,
                useAzureStorage,
                userProfileProvider,
                converterDataMergeService,
                refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
                DatasetsConverterDatasetMerge,
                Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
                IsSessionsEnabled = true)]
            ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}