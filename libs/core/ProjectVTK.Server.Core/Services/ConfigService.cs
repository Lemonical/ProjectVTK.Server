using Microsoft.Extensions.Logging;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Shared.Helpers;
using System.Text.Json;

namespace ProjectVTK.Server.Core.Services;

public class ConfigService
{
    private readonly ILogger<ConfigService> _logger;

    public ServerConfigs? Server { get; private set; }

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
        LoadConfig();
    }

    public void LoadConfig()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "data", "configs.json");

        try
        {
            if (!File.Exists(filePath))
                SaveConfig(new());

            Server = JsonSerializer.Deserialize<ServerConfigs>(File.ReadAllText(filePath), JsonHelper.GetSerializerOptions(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configs: {configFile}", Path.GetFileName(filePath));
        }
    }

    public void SaveConfig(ServerConfigs configs)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "data", "configs.json");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(configs, JsonHelper.GetSerializerOptions(true)));
    }
}
