using Microsoft.Extensions.Logging;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Shared.Helpers;
using ProjectVTK.Shared.Models.Interfaces;
using System.Text.Json;

namespace ProjectVTK.Server.Core.Services;

public class ConfigService
{
    private readonly ILogger<ConfigService> _logger;

    public ServerConfigs? Server { get; private set; }
    public BanList? Bans { get; private set; }

    private List<ICharacter> _characters = [];
    private List<IMusic> _music = [];
    private List<IArea> _areas = [];
    public IReadOnlyCollection<ICharacter> Characters => _characters;
    public IReadOnlyCollection<IMusic> Music => _music;
    public IReadOnlyCollection<IArea> Areas => _areas;

    private readonly string _serverConfigPath = Path.Combine(AppContext.BaseDirectory, "data", "configs.json");
    private readonly string _banListPath = Path.Combine(AppContext.BaseDirectory, "data", "bans.json");

    private string CharactersPath => Path.Combine(AppContext.BaseDirectory, "data", "scenes", Server?.Metadata.Scene ?? "Default", "characters.json");
    private string MusicPath => Path.Combine(AppContext.BaseDirectory, "data", "scenes", Server?.Metadata.Scene ?? "Default", "music.json");
    private string AreasPath => Path.Combine(AppContext.BaseDirectory, "data", "scenes", Server?.Metadata.Scene ?? "Default", "areas.json");

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
        Reload();
    }

    public void Reload()
    {
        Server = LoadConfig<ServerConfigs>(_serverConfigPath) ?? new ServerConfigs();
        Bans = LoadConfig<BanList>(_banListPath) ?? new BanList();

        _characters = LoadConfig<List<ICharacter>>(CharactersPath) ?? [];
        _music = LoadConfig<List<IMusic>>(MusicPath) ?? [];
        _areas = LoadConfig<List<IArea>>(AreasPath) ?? [];
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
            IEnumerable<ICharacter> => CharactersPath,
            IEnumerable<IMusic> => MusicPath,
            IEnumerable<IArea> => AreasPath,
            _ => throw new ArgumentException("Invalid config type", nameof(configs))
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(configs, JsonHelper.GetSerializerOptions(true)));
    }

    public bool AddBannedUsernames(params string[] usernames)
    {
        Bans ??= new BanList();
        var array = Bans.IpAddresses;
        var hasSuccess = ModifyBanList(ref array, usernames, true);
        if (hasSuccess)
        {
            Bans.IpAddresses = array;
            Save(Bans);
        }
        return hasSuccess;
    }

    public bool AddBannedIpAddresses(params string[] ipAddresses)
    {
        Bans ??= new BanList();
        var array = Bans.IpAddresses;
        var hasSuccess = ModifyBanList(ref array, ipAddresses, true);
        if (hasSuccess)
        {
            Bans.IpAddresses = array;
            Save(Bans);
        }
        return hasSuccess;
    }

    public bool RemoveBannedUsernames(params string[] usernames)
    {
        Bans ??= new BanList();
        var array = Bans.IpAddresses;
        var hasSuccess = ModifyBanList(ref array, usernames, false);
        if (hasSuccess)
        {
            Bans.IpAddresses = array;
            Save(Bans);
        }
        return hasSuccess;
    }

    public bool RemoveBannedIpAddresses(params string[] ipAddresses)
    {
        Bans ??= new BanList();
        var array = Bans.IpAddresses;
        var hasSuccess = ModifyBanList(ref array, ipAddresses, false);
        if (hasSuccess)
        {
            Bans.IpAddresses = array;
            Save(Bans);
        }
        return hasSuccess;
    }

    private bool ModifyBanList(ref string[] banList, string[] items, bool add)
    {
        var hasSuccess = false;
        foreach (var item in items)
        {
            var index = Array.IndexOf(banList, item);
            if (add)
            {
                // Does not exist in the array
                if (index == -1)
                {
                    banList = [.. banList, item];
                    hasSuccess = true;
                }
            }
            else
            {
                if (index != -1)
                {
                    banList = banList.Where((_, i) => i != index).ToArray();
                    hasSuccess = true;
                }
            }
        }
        return hasSuccess;
    }
}
