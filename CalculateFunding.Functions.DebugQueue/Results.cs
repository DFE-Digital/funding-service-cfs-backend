using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Utility;
using CalculateFunding.Functions.Results.ServiceBus;
using CalculateFunding.Functions.Results.Timer;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Results.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Results
    {
        private static readonly ILogger log;

        static Results()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(Results));
        }

        [Function("on-merge-specification-information-for-provider-with-results-queue")]
        public static async Task RunOnMergeSpecificationInformationForProviderWithResults([QueueTrigger(ServiceBusConstants.QueueNames.MergeSpecificationInformationForProvider,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

           
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<MergeSpecificationInformationRequest>(item);

            OnMergeSpecificationInformationForProviderWithResults function = scope.ServiceProvider.GetRequiredService<OnMergeSpecificationInformationForProviderWithResults>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-merge-specification-information-for-provider-with-results-failure-queue")]
        public static async Task RunOnMergeSpecificationInformationForProviderWithResultsFailure([QueueTrigger(ServiceBusConstants.QueueNames.MergeSpecificationInformationForProviderPoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
          
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<MergeSpecificationInformationRequest>(item);

            OnMergeSpecificationInformationForProviderWithResultsFailure function = scope.ServiceProvider.GetRequiredService<OnMergeSpecificationInformationForProviderWithResultsFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PopulateCalculationResultsQaDatabase)]
        public static async Task RunOnPopulateCalculationResultsQADatabase([QueueTrigger(ServiceBusConstants.QueueNames.PopulateCalculationResultsQADatabase,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<PopulateCalculationResultQADatabaseRequest>(item);

            OnPopulateCalculationResultsQADatabase function = scope.ServiceProvider.GetRequiredService<OnPopulateCalculationResultsQADatabase>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.PopulateCalculationResultsQaDatabaseFailure)]
        public static async Task RunOnPopulateCalculationResultsQADatabaseFailure([QueueTrigger(ServiceBusConstants.QueueNames.PopulateCalculationResultsQADatabasePoisonedLocal,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<PopulateCalculationResultQADatabaseRequest>(item);

            OnPopulateCalculationResultsQADatabaseFailure function = scope.ServiceProvider.GetRequiredService<OnPopulateCalculationResultsQADatabaseFailure>();

            await function.Run(message);

             log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-reindex-calculation-results-queue")]
        public static async Task RunReIndexCalculationResults([QueueTrigger(ServiceBusConstants.QueueNames.ReIndexCalculationResultsIndex, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReIndexCalculationResults function = scope.ServiceProvider.GetRequiredService<OnReIndexCalculationResults>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-delete-calculation-results-queue")]
        public static async Task RunDeleteCalculationResults([QueueTrigger(ServiceBusConstants.QueueNames.DeleteCalculationResults, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnDeleteCalculationResults function = scope.ServiceProvider.GetRequiredService<OnDeleteCalculationResults>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-calculation-results-csv-generation-queue")]
        public static async Task RunCalculationResultsCsvGeneration([QueueTrigger(ServiceBusConstants.QueueNames.CalculationResultsCsvGeneration, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnCalculationResultsCsvGeneration function = scope.ServiceProvider.GetRequiredService<OnCalculationResultsCsvGeneration>();

            await function.Run(message);

             log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-calculation-results-csv-generation-failure-queue")]
        public static async Task RunCalculationResultsCsvGenerationFailure([QueueTrigger(ServiceBusConstants.QueueNames.CalculationResultsCsvGenerationPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnCalculationResultsCsvGenerationFailure function = scope.ServiceProvider.GetRequiredService<OnCalculationResultsCsvGenerationFailure>();

            await function.Run(message);

             log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-calculation-results-csv-generation-timer-queue")]
        public static async Task RunCalculationResultsCsvGenerationTimer([QueueTrigger(ServiceBusConstants.QueueNames.CalculationResultsCsvGenerationTimer, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            TimerInfo timerInfo = new TimerInfo();

            OnCalculationResultsCsvGenerationTimer function = scope.ServiceProvider.GetRequiredService<OnCalculationResultsCsvGenerationTimer>();

            await function.Run(timerInfo);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.SearchIndexWriter)]
        public static async Task RunOnSearchIndexWriterEventTrigger([QueueTrigger(ServiceBusConstants.QueueNames.SearchIndexWriter, Connection = "StorageConnection")] string item)
        {
            using (IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                ServiceBusReceivedMessage message = Helpers.ConvertToMessage<IEnumerable<string>>(item);

                OnSearchIndexWriterEventTrigger function = scope.ServiceProvider.GetRequiredService<OnSearchIndexWriterEventTrigger>();

                Guard.ArgumentNotNull(function, nameof(OnSearchIndexWriterEventTrigger));

                await function.Run(message);

                log.LogInformation($"C# Queue trigger function processed: {item}");
            }
        }
    }

}
