using Azure.Messaging.ServiceBus;
using CalculateFunding.Functions.CalcEngine.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class CalcEngine
    {
        private static readonly ILogger log;

        static CalcEngine()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(CalcEngine));
        }

        [Function("on-calcs-generate-allocations-event-queue")]
        public static async Task RunOnCalcsCreateDraftEvent(
            [QueueTrigger(ServiceBusConstants.QueueNames.CalcEngineGenerateAllocationResults, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.CalcEngine.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnCalcsGenerateAllocationResults function = scope.ServiceProvider.GetRequiredService<OnCalcsGenerateAllocationResults>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-calcs-generate-allocations-event-poisoned-queue")]
        public static async Task RunOnCalculationGenerateFailure([QueueTrigger(ServiceBusConstants.QueueNames.CalcEngineGenerateAllocationResultsPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.CalcEngine.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnCalculationGenerateFailure function = scope.ServiceProvider.GetRequiredService<OnCalculationGenerateFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }
    }
}
