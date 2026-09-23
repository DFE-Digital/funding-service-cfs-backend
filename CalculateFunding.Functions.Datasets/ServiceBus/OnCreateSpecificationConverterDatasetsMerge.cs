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
    public class OnCreateSpecificationConverterDatasetsMerge : Retriable
    {
        public const string FunctionName = "on-create-specification-converter-data-merge";
        private const string QueueName = ServiceBusConstants.QueueNames.SpecificationConverterDatasetsMerge;
        
        public OnCreateSpecificationConverterDatasetsMerge(ILogger logger,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            ISpecificationConverterDataMerge specificationConverterDatasetsWizard,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger,
                messengerService,
                FunctionName,
                QueueName,
                useAzureStorage,
                userProfileProvider,
                specificationConverterDatasetsWizard,
                refresherProvider)
        {
        }
        
        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
                QueueName,
                Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
                IsSessionsEnabled = true)]
            ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}