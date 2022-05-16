using Megadeth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TdLib;
using TDLib.Bindings;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.Configure<AppSettings>(hostContext.Configuration.GetSection("Configuration"));
        services.AddSingleton(_ =>
        {
            var client = new TdClient();
            client.Bindings.SetLogVerbosityLevel(TdLogLevel.Warning);
            return client;
        });
        services.AddTransient(ser => ser.GetRequiredService<IOptions<AppSettings>>().Value);
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();

