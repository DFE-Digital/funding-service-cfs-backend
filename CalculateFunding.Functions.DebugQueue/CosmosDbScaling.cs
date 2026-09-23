using CalculateFunding.Functions.CosmosDbScaling.Timer;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class CosmosDbScaling
    {
        private static readonly ILogger log;

        static CosmosDbScaling()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(CosmosDbScaling));
        }

        [Function("on-scale-down-cosmosdb-collection-queue")]
        public static async Task RunOnScaleDownCosmosdbCollection([QueueTrigger(ServiceBusConstants.QueueNames.ScaleDownCosmosdbCollection, Connection = "StorageConnection")] string item)
        {
            using (IServiceScope scope = Functions.CosmosDbScaling.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                TimerInfo timerInfo = new TimerInfo();

                OnScaleDownCosmosDbCollection function = scope.ServiceProvider.GetRequiredService<OnScaleDownCosmosDbCollection>();

                await function.Run(timerInfo);

                log.LogInformation($"C# Queue trigger function processed: {item}");
            }
        }

        [Function("on-incremental-scale-down-cosmosdb-collection-queue")]
        public static async Task RunOnIncrementalScaleDownCosmosdbCollection([QueueTrigger(ServiceBusConstants.QueueNames.IncrementalScaleDownCosmosdbCollection, Connection = "StorageConnection")] string item)
        {
            using (IServiceScope scope = Functions.CosmosDbScaling.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                TimerInfo timerInfo = new TimerInfo();

                OnIncrementalScaleDownCosmosDbCollection function = scope.ServiceProvider.GetRequiredService<OnIncrementalScaleDownCosmosDbCollection>();

                await function.Run(timerInfo);

                log.LogInformation($"C# Queue trigger function processed: {item}");
            }
        }
    }
}
