using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
#if DEBUG
        logging.AddDebug();
#endif
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((hostContext, services) =>
    {
        // Add services here
    })
    .Build().Run();