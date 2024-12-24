using ProjectVTK.Shared.Models;

namespace ProjectVTK.Server.Core.Models.SceneObjects;

public class Area(
    ushort id, string name,
    string fileName, ushort userCount,
    string password = "", ushort? maxUserCount = null) : AreaBase(id, name, fileName, userCount)
{
    public ushort? MaxUserCount { get; set; } = maxUserCount;

    public string Password { get; set; } = password;

    public bool IsPasswordProtected => !string.IsNullOrWhiteSpace(Password);
}
