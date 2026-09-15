using CalculateFunding.Services.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var host = new HostBuilder()
    
    .ConfigureFunctionsWorkerDefaults()
  
    .ConfigureServices((context, services) =>
    {
        services.AddLogging("CalculateFunding.Functions.DebugQueue1");

    })
    .Build();

host.Run();
