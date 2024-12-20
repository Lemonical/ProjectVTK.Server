using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectVTK.Server.CLI.Commands.ReceiveHandlers;
using ProjectVTK.Server.CLI.Services;
using ProjectVTK.Server.Core.Services;
using ProjectVTK.Shared.Commands;

Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
#if DEBUG
        logging.AddDebug();
        logging.SetMinimumLevel(LogLevel.Debug);
#else
        logging.SetMinimumLevel(LogLevel.Information);
#endif
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddSingleton<CommandHandlerFactory>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ServerSessions>();
        services.AddSingleton<ServerService>();
        services.AddSingleton<CommandService>();

        services.AddSingleton<ICommandHandler, VersionCheckCommandHandler>();

        services.AddHostedService<ServerBackgroundService>();
    })
    .Build().Run();