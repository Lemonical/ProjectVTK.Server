using Fleck;
using Kokuban;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectVTK.Server.CLI;
using ProjectVTK.Server.CLI.Commands.ConsoleInput;
using ProjectVTK.Server.CLI.Services;
using ProjectVTK.Server.Core;
using ProjectVTK.Server.Core.Services;
using ProjectVTK.Shared.Commands;
using ZLogger;

Console.Title = "Server CLI";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        logging.AddZLoggerConsole(options => options.UsePlainTextFormatter(format =>
        {
            // :short - TRC, DBG, INF, WRN, ERR, CRI, NON
            format.SetPrefixFormatter($"{0} [{1:local-timeonly}]: ",
                (in MessageTemplate template, in LogInfo info) =>
                    template.Format(
                    info.LogLevel.ToString()
                        .Replace("Trace", Chalk.BrightBlue + "TRC")
                        .Replace("Debug", Chalk.BrightMagenta + "DBG")
                        .Replace("Information", Chalk.Green + "INF")
                        .Replace("Warning", Chalk.Bold.Red + "WRN")
                        .Replace("Error", Chalk.Bold.BrightWhite.BgRed + "ERR")
                        .Replace("Critical", Chalk.Bold.BrightWhite.BgRed + "CRI")
                        .Replace("None", "NON"),
                    info.Timestamp
                ));
        }));
        logging.AddZLoggerRollingFile(options =>
        {
            options.UsePlainTextFormatter(format =>
                format.SetPrefixFormatter($"{0:short} [{1:local-timeonly}]: ",
                    (in MessageTemplate template, in LogInfo info) =>
                        template.Format(info.LogLevel, info.Timestamp)));
            options.FilePathSelector = (dt, index) => $"logs{Path.DirectorySeparatorChar}{dt:dd-MM-yyyy}_{index}.log";
            options.RollingSizeKB = 10240 * 1024;
        });
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddSingleton<CommandHandlerFactory>();
        services.AddSingleton<ConsoleCommandHandler>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ServerSessions>();
        services.AddSingleton<CommandService>();
        services.AddSingleton<ServerService>();
        services.AddSingleton<CustomFleckLogger>();
        services.AddSingleton<ServerState>();

        services.AddHostedService<ServerBackgroundService>();
    })
    .Build();

// Override Fleck logging
var services = host.Services;
var customFleckLogger = services.GetRequiredService<CustomFleckLogger>();
FleckLog.LogAction = customFleckLogger.Log;

host.Run();