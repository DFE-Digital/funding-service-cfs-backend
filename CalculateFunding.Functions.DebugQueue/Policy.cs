using Azure.Messaging.ServiceBus;
using CalculateFunding.Functions.Policy.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Policy
    {
        private static readonly ILogger log;

        static Policy()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(Policy));
        }

        [Function("on-policy-reindex-templates-queue")]
        public static async Task RunReIndexTemplate([QueueTrigger(ServiceBusConstants.QueueNames.PolicyReIndexTemplates,
                Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Policy.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnReIndexTemplates function = scope.ServiceProvider.GetRequiredService<OnReIndexTemplates>();

            await function.Run(message);

           log.LogInformation($"C# Queue trigger function processed: {item}");
        }
    }
}
