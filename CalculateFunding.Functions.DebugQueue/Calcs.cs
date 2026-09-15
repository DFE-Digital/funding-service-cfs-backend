using Azure.Messaging.ServiceBus;
using CalculateFunding.Functions.Calcs.ServiceBus;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Calcs
    {
        private static readonly ILogger log;

        static Calcs()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(Calcs));
        }
        [Function("on-update-code-context-cache-poisoned-queue")]
        public static async Task RunUpdateCodeContextCacheFailure([QueueTrigger(ServiceBusConstants.QueueNames.UpdateCodeContextCachePoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<DatasetRelationshipSummary>(item);

            OnUpdateCodeContextCacheFailure function = scope.ServiceProvider.GetRequiredService<OnUpdateCodeContextCacheFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-update-code-context-cache-queue")]
        public static async Task RunUpdateCodeContextCache([QueueTrigger(ServiceBusConstants.QueueNames.UpdateCodeContextCache, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnUpdateCodeContextCache function = scope.ServiceProvider.GetRequiredService<OnUpdateCodeContextCache>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-calcs-add-data-relationship-queue")]
        public static async Task RunCalcsAddRelationshipToBuildProject([QueueTrigger(ServiceBusConstants.QueueNames.UpdateBuildProjectRelationships, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<DatasetRelationshipSummary>(item);

            CalcsAddRelationshipToBuildProject function = scope.ServiceProvider.GetRequiredService<CalcsAddRelationshipToBuildProject>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-calcs-instruct-allocations-queue")]
        public static async Task RunOnCalcsInstructAllocationResults([QueueTrigger(ServiceBusConstants.QueueNames.CalculationJobInitialiser, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<Dataset>(item);

            OnCalcsInstructAllocationResults function = scope.ServiceProvider.GetRequiredService<OnCalcsInstructAllocationResults>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-delete-calculations-queue")]
        public static async Task RunOnDeleteCalculations([QueueTrigger(ServiceBusConstants.QueueNames.DeleteCalculations, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnDeleteCalculations function = scope.ServiceProvider.GetRequiredService<OnDeleteCalculations>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-calcs-instruct-allocations-poisoned-queue")]
        public static async Task RunOnCalcsInstructAllocationResultsFailure([QueueTrigger(ServiceBusConstants.QueueNames.CalculationJobInitialiserPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnCalcsInstructAllocationResultsFailure function = scope.ServiceProvider.GetRequiredService<OnCalcsInstructAllocationResultsFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-apply-template-calculations-queue")]
        public static async Task RunOnApplyTemplateCalculations([QueueTrigger(ServiceBusConstants.QueueNames.ApplyTemplateCalculations, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnApplyTemplateCalculations function = scope.ServiceProvider.GetRequiredService<OnApplyTemplateCalculations>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-apply-template-calculations-poisoned-queue")]
        public static async Task RunOnApplyTemplateCalculationsFailure([QueueTrigger(ServiceBusConstants.QueueNames.ApplyTemplateCalculationsPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnApplyTemplateCalculationsFailure function = scope.ServiceProvider.GetRequiredService<OnApplyTemplateCalculationsFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-reindex-specification-calculation-relationships-queue")]
        public static async Task RunOnReIndexSpecificationCalculationRelationships([QueueTrigger(ServiceBusConstants.QueueNames.ReIndexSpecificationCalculationRelationships, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReIndexSpecificationCalculationRelationships function = scope.ServiceProvider.GetRequiredService<OnReIndexSpecificationCalculationRelationships>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-reindex-specification-calculation-relationships-poisoned-queue")]
        public static async Task RunOnReIndexSpecificationCalculationRelationshipsFailure([QueueTrigger(ServiceBusConstants.QueueNames.ReIndexSpecificationCalculationRelationshipsPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReIndexSpecificationCalculationRelationshipsFailure function = scope.ServiceProvider.GetRequiredService<OnReIndexSpecificationCalculationRelationshipsFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-approve-all-calculations-queue")]
        public static async Task RunOnApproveAllCalculations(
            [QueueTrigger(ServiceBusConstants.QueueNames.ApproveAllCalculations, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnApproveAllCalculations function = scope.ServiceProvider.GetRequiredService<OnApproveAllCalculations>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-approve-all-calculations-poisoned-queue")]
        public static async Task RunOnApproveAllCalculationsFailure(
            [QueueTrigger(ServiceBusConstants.QueueNames.ApproveAllCalculationsPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnApproveAllCalculationsFailure function = scope.ServiceProvider.GetRequiredService<OnApproveAllCalculationsFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-referenced-specification-remap-queue")]
        public static async Task RunOnReferencedSpecificationReMap(
            [QueueTrigger(ServiceBusConstants.QueueNames.ReferencedSpecificationReMap, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReferencedSpecificationReMap function = scope.ServiceProvider.GetRequiredService<OnReferencedSpecificationReMap>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-referenced-specification-remap-poisoned-queue")]
        public static async Task RunOnReferencedSpecificationReMapFailure(
            [QueueTrigger(ServiceBusConstants.QueueNames.ReferencedSpecificationReMapPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReferencedSpecificationReMapFailure function = scope.ServiceProvider.GetRequiredService<OnReferencedSpecificationReMapFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }
    }
}
