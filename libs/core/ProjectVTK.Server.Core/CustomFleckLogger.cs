using Microsoft.Extensions.Logging;

namespace ProjectVTK.Server.Core;

public class CustomFleckLogger(ILogger<CustomFleckLogger> logger)
{
    private readonly ILogger<CustomFleckLogger> _logger = logger;

    public void Log(Fleck.LogLevel level, string message, Exception ex)
    {
        var logLevel = level switch
        {
            Fleck.LogLevel.Debug => LogLevel.Debug,
            Fleck.LogLevel.Info => LogLevel.Information,
            Fleck.LogLevel.Warn => LogLevel.Warning,
            Fleck.LogLevel.Error => LogLevel.Error,
            _ => LogLevel.None
        };

        if (ex != null)
            _logger.Log(logLevel, ex, "{msg}", message);
        else
            _logger.Log(logLevel, "{msg}", message);
    }
}