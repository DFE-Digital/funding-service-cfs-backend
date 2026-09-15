using Microsoft.Extensions.Hosting;
using CalculateFunding.Functions.Publishing;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        Startup.RegisterComponents(services, config);

    })
    .Build();

host.Run();
