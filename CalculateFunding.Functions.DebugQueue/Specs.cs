using Azure.Messaging.ServiceBus;
using CalculateFunding.Functions.Specs.ServiceBus;
using CalculateFunding.Models.Messages;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Specs
    {
        private static readonly ILogger log;

        static Specs()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(Specs));
        }

        [Function("on-detect-obsolete-funding-lines-queue")]
        public static async Task RunDetectObsoleteFundingLines([QueueTrigger(ServiceBusConstants.QueueNames.DetectObsoleteFundingLines, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Specs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ServiceBusReceivedMessage>(item);

            OnDetectObsoleteFundingLines function = scope.ServiceProvider.GetRequiredService<OnDetectObsoleteFundingLines>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-detect-obsolete-funding-lines-failure-queue")]
        public static async Task RunDetectObsoleteFundingLinesFailure([QueueTrigger(ServiceBusConstants.QueueNames.DetectObsoleteFundingLinesPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Specs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ServiceBusReceivedMessage>(item);

            OnDetectObsoleteFundingLinesFailure function = scope.ServiceProvider.GetRequiredService<OnDetectObsoleteFundingLinesFailure>();

            await function.Run(message);

           log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-add-relationship-event-queue")]
        public static async Task RunAddRelationship([QueueTrigger(ServiceBusConstants.QueueNames.AddDefinitionRelationshipToSpecification, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Specs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<AssignDefinitionRelationshipMessage>(item);

            OnAddRelationshipEvent function = scope.ServiceProvider.GetRequiredService<OnAddRelationshipEvent>();

            await function.Run(message);

             log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-reindex-specification-queue")]
        public static async Task RunReIndexSpec([QueueTrigger(ServiceBusConstants.QueueNames.ReIndexSingleSpecification, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Specs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ServiceBusReceivedMessage>(item);

            OnReIndexSpecification function = scope.ServiceProvider.GetRequiredService<OnReIndexSpecification>();

            await function.Run(message);

             log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-delete-specifications-queue")]
        public static async Task RunDeleteSpecs([QueueTrigger(ServiceBusConstants.QueueNames.DeleteSpecifications, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Specs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnDeleteSpecifications function = scope.ServiceProvider.GetRequiredService<OnDeleteSpecifications>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }
    }
}
