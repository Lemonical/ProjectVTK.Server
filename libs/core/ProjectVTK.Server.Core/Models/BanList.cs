namespace ProjectVTK.Server.Core.Models;

public record BanList
{
    public string[] Usernames { get; set; } = [];
    public string[] IpAddresses { get; set; } = [];
}