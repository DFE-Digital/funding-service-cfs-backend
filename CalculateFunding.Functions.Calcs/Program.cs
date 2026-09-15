using CalculateFunding.Functions.Calcs;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        Startup.RegisterComponents(services, config);

    })
    .Build();

host.Run();