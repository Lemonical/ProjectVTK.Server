using ProjectVTK.Shared.Models;

namespace ProjectVTK.Server.Core.Models.SceneObjects;

public class Music(ushort id, string name, string fileName) : MusicBase(id, name, fileName)
{
    // TODO: Time started playing
}