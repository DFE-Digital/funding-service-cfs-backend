using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Utility;
using CalculateFunding.Functions.Datasets;
using CalculateFunding.Functions.Datasets.ServiceBus;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Converter;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Datasets
    {
        private static readonly ILogger log;

        static Datasets()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(Datasets));
        }

        [Function("on-dataset-event-queue")]
        public static async Task RunPublishProviderResults([QueueTrigger(ServiceBusConstants.QueueNames.ProcessDataset, Connection = "StorageConnection")]
            string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            CultureInfo.CurrentCulture = new CultureInfo("en-GB");
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<Dataset>(item);

            OnDatasetEvent function = scope.ServiceProvider.GetRequiredService<OnDatasetEvent>();

            Guard.ArgumentNotNull(function, nameof(OnDatasetEvent));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-delete-datasets-queue")]
        public static async Task RunDeleteDatasets([QueueTrigger(ServiceBusConstants.QueueNames.DeleteDatasets, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            CultureInfo.CurrentCulture = new CultureInfo("en-GB");
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnDeleteDatasets function = scope.ServiceProvider.GetRequiredService<OnDeleteDatasets>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-dataset-event-poisoned-queue")]
        public static async Task RunPublishProviderResultsFailure([QueueTrigger(ServiceBusConstants.QueueNames.ProcessDatasetPoisonedLocal, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<Dataset>(item);

            OnDatasetEventFailure function = scope.ServiceProvider.GetRequiredService<OnDatasetEventFailure>();

            Guard.ArgumentNotNull(function, nameof(OnDatasetEventFailure));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-dataset-validation-event-queue")]
        public static async Task RunValidateDatasetEvent([QueueTrigger(ServiceBusConstants.QueueNames.ValidateDataset, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            CultureInfo.CurrentCulture = new CultureInfo("en-GB");
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<GetDatasetBlobModel>(item);

            OnDatasetValidationEvent function = scope.ServiceProvider.GetRequiredService<OnDatasetValidationEvent>();

            Guard.ArgumentNotNull(function, nameof(OnDatasetValidationEvent));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-dataset-validation-event-poisoned-queue")]
        public static async Task RunOnValidateDatasetsFailure([QueueTrigger(ServiceBusConstants.QueueNames.ValidateDatasetPoisonedLocal, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnDatasetValidationEventFailure function = scope.ServiceProvider.GetRequiredService<OnDatasetValidationEventFailure>();

            Guard.ArgumentNotNull(function, nameof(OnDatasetValidationEventFailure));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.MapFdzDatasets)]
        public static async Task RunOnMapFdzDatasetsEventFired([QueueTrigger(ServiceBusConstants.QueueNames.MapFdzDatasets, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<Dataset>(item);

            OnMapFdzDatasetsEventFired function = scope.ServiceProvider.GetRequiredService<OnMapFdzDatasetsEventFired>();

            Guard.ArgumentNotNull(function, nameof(OnMapFdzDatasetsEventFired));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.MapFdzDatasetsPoisoned)]
        public static async Task RunOnMapFdzDatasetsEventFiredFailure([QueueTrigger(ServiceBusConstants.QueueNames.MapFdzDatasetsPoisonedLocal, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<Dataset>(item);

            OnMapFdzDatasetsEventFiredFailure function = scope.ServiceProvider.GetRequiredService<OnMapFdzDatasetsEventFiredFailure>();

            Guard.ArgumentNotNull(function, nameof(OnMapFdzDatasetsEventFiredFailure));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-run-converter-data-merge-queue")]
        public static async Task RunOnRunConverterDataMerge([QueueTrigger(ServiceBusConstants.QueueNames.RunConverterDatasetMerge, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ConverterMergeRequest>(item);

            OnRunConverterDataMerge function = scope.ServiceProvider.GetRequiredService<OnRunConverterDataMerge>();

            Guard.ArgumentNotNull(function, nameof(OnMapFdzDatasetsEventFired));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-run-converter-data-merge-failure-queue")]
        public static async Task RunOnRunConverterDataMergeFailure(
            [QueueTrigger(ServiceBusConstants.QueueNames.RunConverterDatasetMergePoisonedLocal, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ConverterMergeRequest>(item);

            OnRunConverterDataMergeFailure function = scope.ServiceProvider.GetRequiredService<OnRunConverterDataMergeFailure>();

            Guard.ArgumentNotNull(function, nameof(OnMapFdzDatasetsEventFiredFailure));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-create-specification-converter-data-merge-queue")]
        public static async Task RunOnCreateSpecificationConverterDatasetsMerge([QueueTrigger(ServiceBusConstants.QueueNames.SpecificationConverterDatasetsMerge, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<SpecificationConverterMergeRequest>(item);

            OnCreateSpecificationConverterDatasetsMerge function = scope.ServiceProvider.GetRequiredService<OnCreateSpecificationConverterDatasetsMerge>();

            Guard.ArgumentNotNull(function, nameof(OnCreateSpecificationConverterDatasetsMerge));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-create-specification-converter-data-merge-failure-queue")]
        public static async Task RunOnCreateSpecificationConverterDatasetsMergeFailure(
            [QueueTrigger(ServiceBusConstants.QueueNames.SpecificationConverterDatasetsMergePoisonedLocal, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<SpecificationConverterMergeRequest>(item);

            OnCreateSpecificationConverterDatasetsMergeFailure function = scope.ServiceProvider.GetRequiredService<OnCreateSpecificationConverterDatasetsMergeFailure>();

            Guard.ArgumentNotNull(function, nameof(OnCreateSpecificationConverterDatasetsMergeFailure));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-converter-wizard-activity-csv-generation-queue")]
        public static async Task RunOnConverterWizardActivityCsvGeneration([QueueTrigger(ServiceBusConstants.QueueNames.ConverterWizardActivityCsvGeneration, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ConverterMergeRequest>(item);

            OnConverterWizardActivityCsvGeneration function = scope.ServiceProvider.GetRequiredService<OnConverterWizardActivityCsvGeneration>();

            Guard.ArgumentNotNull(function, nameof(OnConverterWizardActivityCsvGeneration));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-converter-wizard-activity-csv-generation-poisoned-queue")]
        public static async Task RunOnConverterWizardActivityCsvGenerationFailure(
            [QueueTrigger(ServiceBusConstants.QueueNames.ConverterWizardActivityCsvGenerationPoisonedLocal, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ConverterMergeRequest>(item);

            OnConverterWizardActivityCsvGenerationFailure function = scope.ServiceProvider.GetRequiredService<OnConverterWizardActivityCsvGenerationFailure>();

            Guard.ArgumentNotNull(function, nameof(OnConverterWizardActivityCsvGenerationFailure));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }



        [Function("on-process-dataset-obsolete-items-queue")]
        public static async Task RunOnProcessDatasetObsoleteItems(
            [QueueTrigger(ServiceBusConstants.QueueNames.ProcessDatasetObsoleteItems, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnProcessDatasetObsoleteItems function = scope.ServiceProvider.GetRequiredService<OnProcessDatasetObsoleteItems>();

            Guard.ArgumentNotNull(function, nameof(OnProcessDatasetObsoleteItems));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-process-dataset-obsolete-items-poisoned-queue")]
        public static async Task RunOnProcessDatasetObsoleteItemsFailure(
            [QueueTrigger(ServiceBusConstants.QueueNames.ProcessDatasetObsoleteItemsPoisonedLocal, Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnProcessDatasetObsoleteItemsFailure function = scope.ServiceProvider.GetRequiredService<OnProcessDatasetObsoleteItemsFailure>();

            Guard.ArgumentNotNull(function, nameof(OnProcessDatasetObsoleteItemsFailure));

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }
    }
}
