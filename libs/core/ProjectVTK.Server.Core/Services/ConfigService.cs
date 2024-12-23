using Microsoft.Extensions.Logging;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Shared.Helpers;
using System.Text.Json;

namespace ProjectVTK.Server.Core.Services;

public class ConfigService
{
    private readonly ILogger<ConfigService> _logger;

    public ServerConfigs? Server { get; private set; }
    public BanList? Bans { get; private set; }

    private readonly string _serverConfigPath = Path.Combine(AppContext.BaseDirectory, "data", "configs.json");
    private readonly string _banListPath = Path.Combine(AppContext.BaseDirectory, "data", "bans.json");

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
        Reload();
    }

    public void Reload()
    {
        Server = LoadConfig<ServerConfigs>(_serverConfigPath) ?? new ServerConfigs();
        Bans = LoadConfig<BanList>(_banListPath) ?? new BanList();
    }

    private T? LoadConfig<T>(string path) where T : class, new()
    {
        try
        {
            if (!File.Exists(path))
                Save(new T());

            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonHelper.GetSerializerOptions(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configs: {configFile}", Path.GetFileName(path));
            return null;
        }
    }

    public void Save(object configs)
    {
        string path = configs switch
        {
            ServerConfigs => _serverConfigPath,
            BanList => _banListPath,
            _ => throw new ArgumentException("Invalid config type", nameof(configs))
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(configs, JsonHelper.GetSerializerOptions(true)));
    }

    public bool AddBannedUsernames(params string[] usernames)
    {
        Bans ??= new BanList();
        var bans = Bans.Usernames;
        var hasSuccess = false;
        foreach (var username in usernames)
        {
            if (bans.FirstOrDefault(b => b.Equals(username, StringComparison.OrdinalIgnoreCase)) is not null)
                continue;
            // Add username to the end of the array
            bans = [.. bans, username];
            hasSuccess = true;
        }
        Save(Bans);
        Bans.Usernames = bans;
        return hasSuccess;
    }

    public bool AddBannedIpAddresses(params string[] ipAddresses)
    {
        Bans ??= new BanList();
        var bans = Bans.IpAddresses;
        var hasSuccess = false;
        foreach (var ip in ipAddresses)
        {
            if (bans.FirstOrDefault(b => b.Equals(ip, StringComparison.OrdinalIgnoreCase)) is not null)
                continue;
            // Add IP address to the end of the array
            bans = [.. bans, ip];
            hasSuccess = true;
        }
        Save(Bans);
        Bans.IpAddresses = bans;
        return hasSuccess;
    }

    public bool RemoveBannedUsernames(params string[] usernames)
    {
        Bans ??= new BanList();
        var bans = Bans.Usernames;
        var hasSuccess = false;
        foreach (var username in usernames)
        {
            var index = Array.IndexOf(bans, username);
            if (index == -1)
                continue;
            // Remove username from the array
            bans = bans.Where((_, i) => i != index).ToArray();
            hasSuccess = true;
        }
        Save(Bans);
        Bans.Usernames = bans;
        return hasSuccess;
    }

    public bool RemoveBannedIpAddresses(params string[] ipAddresses)
    {
        Bans ??= new BanList();
        var bans = Bans.IpAddresses;
        var hasSuccess = false;
        foreach (var ip in ipAddresses)
        {
            var index = Array.IndexOf(bans, ip);
            if (index == -1)
                continue;
            // Remove IP address from the array
            bans = bans.Where((_, i) => i != index).ToArray();
            hasSuccess = true;
        }
        Save(Bans);
        Bans.IpAddresses = bans;
        return hasSuccess;
    }
}
