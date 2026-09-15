using Azure.Messaging.ServiceBus;
using CalculateFunding.Functions.Publishing.ServiceBus;
using CalculateFunding.Models.Publishing.FundingManagement;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Publishing.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace CalculateFunding.Functions.DebugQueue
{
    public static class Publishing
    {
        private static readonly ILogger log;

        static Publishing()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(Publishing));
        }

        [Function(FunctionConstants.BatchPublishedProviderValidation)]
        public static async Task RunBatchPublishedProviderValidation([QueueTrigger(ServiceBusConstants.QueueNames.PublishingBatchPublishedProviderValidation,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnBatchPublishedProviderValidation function = scope.ServiceProvider.GetRequiredService<OnBatchPublishedProviderValidation>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.BatchPublishedProviderValidationPoisoned)]
        public static async Task RunBatchPublishedProviderValidationFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingBatchPublishedProviderValidationPoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnBatchPublishedProviderValidationFailure function = scope.ServiceProvider.GetRequiredService<OnBatchPublishedProviderValidationFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingRunSqlImport)]
        public static async Task RunSqlImport([QueueTrigger(ServiceBusConstants.QueueNames.PublishingRunSqlImport,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnRunSqlImport function = scope.ServiceProvider.GetRequiredService<OnRunSqlImport>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-run-sql-import-failure-queue")]
        public static async Task RunSqlImportFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingRunSqlImportPoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnRunSqlImportFailure function = scope.ServiceProvider.GetRequiredService<OnRunSqlImportFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingRunReleasedSqlImport)]
        public static async Task RunReleasedSqlImport([QueueTrigger(ServiceBusConstants.QueueNames.PublishingRunReleasedSqlImport,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnRunReleasedSqlImport function = scope.ServiceProvider.GetRequiredService<OnRunReleasedSqlImport>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-run-released-sql-import-failure-queue")]
        public static async Task RunReleasedSqlImportFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingRunReleasedSqlImportPoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnRunReleasedSqlImportFailure function = scope.ServiceProvider.GetRequiredService<OnRunReleasedSqlImportFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-published-funding-undo-queue")]
        public static async Task RunUndoPublishedFunding([QueueTrigger(ServiceBusConstants.QueueNames.PublishedFundingUndo,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPublishedFundingUndo function = scope.ServiceProvider.GetRequiredService<OnPublishedFundingUndo>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-generate-published-funding-csv-queue")]
        public static async Task RunGeneratePublishedFundingCsv([QueueTrigger(ServiceBusConstants.QueueNames.GeneratePublishedFundingCsv,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnGeneratePublishedFundingCsv function = scope.ServiceProvider.GetRequiredService<OnGeneratePublishedFundingCsv>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-generate-published-funding-csv-failure-queue")]
        public static async Task RunGeneratePublishedFundingCsvFailure([QueueTrigger(ServiceBusConstants.QueueNames.GeneratePublishedFundingCsvPoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnGeneratePublishedFundingCsvFailure function = scope.ServiceProvider.GetRequiredService<OnGeneratePublishedFundingCsvFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-delete-published-providers-queue")]
        public static async Task RunDeletePublishedProviders([QueueTrigger(ServiceBusConstants.QueueNames.DeletePublishedProviders,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnDeletePublishedProviders function = scope.ServiceProvider.GetRequiredService<OnDeletePublishedProviders>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-reindex-published-providers-queue")]
        public static async Task RunReIndexPublishedProviders([QueueTrigger(ServiceBusConstants.QueueNames.PublishingReIndexPublishedProviders,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReIndexPublishedProviders function = scope.ServiceProvider.GetRequiredService<OnReIndexPublishedProviders>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-reindex-published-providers-poisoned-queue")]
        public static async Task RunReIndexPublishedProvidersFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingReIndexPublishedProvidersPoisonedLocal,
                    Connection = "StorageConnection")]
                string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReIndexPublishedProvidersFailure function = scope.ServiceProvider.GetRequiredService<OnReIndexPublishedProvidersFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingApproveAllProviderFunding)]
        public static async Task RunApproveAllProviderFunding([QueueTrigger(ServiceBusConstants.QueueNames.PublishingApproveAllProviderFunding, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnApproveAllProviderFunding function = scope.ServiceProvider.GetRequiredService<OnApproveAllProviderFunding>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingApproveAllProviderFundingPoisoned)]
        public static async Task RunApproveAllProviderFundingFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingApproveAllProviderFundingPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnApproveAllProviderFundingFailure function = scope.ServiceProvider.GetRequiredService<OnApproveAllProviderFundingFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingApproveBatchProviderFunding)]
        public static async Task RunApproveBatchProviderFunding([QueueTrigger(ServiceBusConstants.QueueNames.PublishingApproveBatchProviderFunding, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<PublishedProviderIdsRequest>(item);

            OnApproveBatchProviderFunding function = scope.ServiceProvider.GetRequiredService<OnApproveBatchProviderFunding>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingApproveBatchProviderFundingPoisoned)]
        public static async Task RunApproveBatchProviderFundingFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingApproveBatchProviderFundingPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnApproveBatchProviderFundingFailure function = scope.ServiceProvider.GetRequiredService<OnApproveBatchProviderFundingFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingRefreshFunding)]
        public static async Task RunRefreshFunding([QueueTrigger(ServiceBusConstants.QueueNames.PublishingRefreshFunding, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnRefreshFunding function = scope.ServiceProvider.GetRequiredService<OnRefreshFunding>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingRefreshFundingPoisoned)]
        public static async Task RunRefreshFundingFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingRefreshFundingPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnRefreshFundingFailure function = scope.ServiceProvider.GetRequiredService<OnRefreshFundingFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }


        [Function(FunctionConstants.ReprofilingOnDemand)]
        public static async Task RunReprofilingOnDemand([QueueTrigger(ServiceBusConstants.QueueNames.ReprofilingOnDemand, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<PublishedProviderIdsRequest>(item);

            OnReprofilingOnDemand function = scope.ServiceProvider.GetRequiredService<OnReprofilingOnDemand>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.ReprofilingOnDemandPoisoned)]
        public static async Task RunReprofilingOnDemandFailure([QueueTrigger(ServiceBusConstants.QueueNames.ReprofilingOnDemandPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReprofilingOnDemandFailure function = scope.ServiceProvider.GetRequiredService<OnReprofilingOnDemandFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingPublishAllProviderFunding)]
        public static async Task RunPublishAllProviderFunding([QueueTrigger(ServiceBusConstants.QueueNames.PublishingPublishAllProviderFunding, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPublishAllProviderFunding function = scope.ServiceProvider.GetRequiredService<OnPublishAllProviderFunding>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingPublishAllProviderFundingPoisoned)]
        public static async Task RunPublishAllProviderFundingFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingPublishAllProviderFundingPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPublishAllProviderFundingFailure function = scope.ServiceProvider.GetRequiredService<OnPublishAllProviderFundingFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishIntegrityCheck)]
        public static async Task RunPublishIntegrityCheck([QueueTrigger(ServiceBusConstants.QueueNames.PublishIntegrityCheck, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPublishIntegrityCheck function = scope.ServiceProvider.GetRequiredService<OnPublishIntegrityCheck>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishIntegrityCheckPoisoned)]
        public static async Task RunPublishIntegrityCheckFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishIntegrityCheckPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPublishIntegrityCheckFailure function = scope.ServiceProvider.GetRequiredService<OnPublishIntegrityCheckFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingPublishBatchProviderFunding)]
        public static async Task RunPublishBatchProviderFunding([QueueTrigger(ServiceBusConstants.QueueNames.PublishingPublishBatchProviderFunding, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<PublishedProviderIdsRequest>(item);

            OnPublishBatchProviderFunding function = scope.ServiceProvider.GetRequiredService<OnPublishBatchProviderFunding>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingPublishBatchProviderFundingPoisoned)]
        public static async Task RunPublishBatchProviderFundingFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingPublishBatchProviderFundingPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPublishBatchProviderFundingFailure function = scope.ServiceProvider.GetRequiredService<OnPublishBatchProviderFundingFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-generate-published-provider-estate-csv-queue")]
        public static async Task RunGeneratePublishedProviderEstateCsv([QueueTrigger(ServiceBusConstants.QueueNames.GeneratePublishedProviderEstateCsv,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnGeneratePublishedProviderEstateCsv function = scope.ServiceProvider.GetRequiredService<OnGeneratePublishedProviderEstateCsv>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-generate-published-provider-estate-csv-failure-queue")]
        public static async Task RunGeneratePublishedProviderEstateCsvFailure([QueueTrigger(ServiceBusConstants.QueueNames.GeneratePublishedProviderEstateCsvPoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnGeneratePublishedFundingCsvFailure function = scope.ServiceProvider.GetRequiredService<OnGeneratePublishedFundingCsvFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-generate-channel-level-published-group-csv-queue")]
        public static async Task RunGenerateChannelLevelPublishedGroupCsv([QueueTrigger(ServiceBusConstants.QueueNames.GenerateChannelLevelPublishedGroupCsv,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnGenerateChannelLevelPublishedGroupCsv function = scope.ServiceProvider.GetRequiredService<OnGenerateChannelLevelPublishedGroupCsv>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-generate-channel-level-published-group-failure-queue")]
        public static async Task RunGenerateChannelLevelPublishedGroupCsvFailure([QueueTrigger(ServiceBusConstants.QueueNames.GenerateChannelLevelPublishedGroupCsvPoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnGenerateChannelLevelPublishedGroupCsvFailure function = scope.ServiceProvider.GetRequiredService<OnGenerateChannelLevelPublishedGroupCsvFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-generate-published-provider-state-summary-csv-queue")]
        public static async Task RunGeneratePublishedProviderStateSummaryCsv([QueueTrigger(ServiceBusConstants.QueueNames.GeneratePublishedProviderStateSummaryCsv,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnGeneratePublishedProviderStateSummaryCsv function = scope.ServiceProvider.GetRequiredService<OnGeneratePublishedProviderStateSummaryCsv>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-publishing-generate-published-provider-state-summary-csv-failure-queue")]
        public static async Task RunGeneratePublishedProviderStateSummaryCsvFailure([QueueTrigger(ServiceBusConstants.QueueNames.GeneratePublishedProviderStateSummaryCsvPoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnGeneratePublishedProviderStateSummaryCsvFailure function = scope.ServiceProvider.GetRequiredService<OnGeneratePublishedProviderStateSummaryCsvFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingDatasetsDataCopy)]
        public static async Task RunDatasetsDataCopy([QueueTrigger(ServiceBusConstants.QueueNames.PublishingDatasetsDataCopy, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPublishDatasetsCopy function = scope.ServiceProvider.GetRequiredService<OnPublishDatasetsCopy>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingDatasetsDataCopyPoisoned)]
        public static async Task RunDatasetsDataCopyFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingDatasetsDataCopyPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPublishDatasetsCopyFailure function = scope.ServiceProvider.GetRequiredService<OnPublishDatasetsCopyFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.ReleaseManagementDataMigration)]
        public static async Task RunReleaseManagementDataMigration([QueueTrigger(ServiceBusConstants.QueueNames.ReleaseManagementDataMigration, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string[]>(item);

            OnReleaseManagementDataMigration function = scope.ServiceProvider.GetRequiredService<OnReleaseManagementDataMigration>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.ReleaseManagementDataMigrationPoisoned)]
        public static async Task RunReleaseManagementDataMigrationFailure([QueueTrigger(ServiceBusConstants.QueueNames.ReleaseManagementDataMigrationPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string[]>(item);

            OnReleaseManagementDataMigrationFailure function = scope.ServiceProvider.GetRequiredService<OnReleaseManagementDataMigrationFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.ProviderGroupingDataBackfilling)]
        public static async Task RunProviderGroupingDataBackfilling([QueueTrigger(ServiceBusConstants.QueueNames.ProviderGroupingDataBackfilling, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string[]>(item);

            OnProviderGroupingDataBackfilling function = scope.ServiceProvider.GetRequiredService<OnProviderGroupingDataBackfilling>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.ProviderGroupingDataBackfillingPoisoned)]
        public static async Task RunProviderGroupingDataBackfillingFailure([QueueTrigger(ServiceBusConstants.QueueNames.ProviderGroupingDataBackfillingPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string[]>(item);

            OnProviderGroupingDataBackfillingFailure function = scope.ServiceProvider.GetRequiredService<OnProviderGroupingDataBackfillingFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingReleaseProvidersToChannels)]
        public static async Task RunReleaseProvidersToChannels([QueueTrigger(ServiceBusConstants.QueueNames.PublishingReleaseProvidersToChannels, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ReleaseProvidersToChannelRequest>(item);

            OnReleaseProvidersToChannels function = scope.ServiceProvider.GetRequiredService<OnReleaseProvidersToChannels>();

            await function.Run(message);

             log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PublishingReleaseProvidersToChannelsPoisoned)]
        public static async Task RunReleaseProvidersToChannelsFailure([QueueTrigger(ServiceBusConstants.QueueNames.PublishingReleaseProvidersToChannelsPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Publishing.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ReleaseProvidersToChannelRequest>(item);

            OnReleaseProvidersToChannelsFailure function = scope.ServiceProvider.GetRequiredService<OnReleaseProvidersToChannelsFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }
    }
}
