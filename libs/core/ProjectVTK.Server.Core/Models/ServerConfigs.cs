namespace ProjectVTK.Server.Core.Models;

public record ServerConfigs
{
    public NetworkSettings Network { get; set; } = new();
    public ServerMetadata Metadata { get; set; } = new();
    public UserManagementSettings UserManagement { get; set; } = new();
    public ChatSettings Chat { get; set; } = new();
}

public record NetworkSettings
{
    public int Port { get; set; } = 27015;
    public uint MaxUsers { get; set; } = 69;
}

public record ServerMetadata
{
    public string Name { get; set; } = "Test Server";
    public string Description { get; set; } = "Just testing, but if you can, do come by and say \"Hello!\" or not.";
    public Uri SiteUrl { get; set; } = new("https://www.google.com");
    public Uri DiscordInviteUrl { get; set; } = new("https://discord.gg/kwqTdrbWtz");
    public string Scene { get; set; } = "Default";
}

public record UserManagementSettings
{
    public bool AutoLogin { get; set; } = true;
    public ushort EnterLockedAreasAuthLevel { get; set; } = 3;
    public ushort EnterFullAreasAuthLevel { get; set; } = 3;
    public ushort IgnoreAreaPasswordAuthLevel { get; set; } = 3;
}

public record ChatSettings
{
    public int DuplicateMessageFilterInterval { get; set; } = 5000;
    public int RatelimitTokens { get; set; } = 10;
    public int RatelimitInterval { get; set; } = 6000;
    public bool SendMotd { get; set; } = true;
    public string Motd { get; set; } = "Welcome to %SERVERNAME%, %USERNAME%! Visit us at https://discord.gg/kwqTdrbWtz, or use /discord in OOC.";
}

