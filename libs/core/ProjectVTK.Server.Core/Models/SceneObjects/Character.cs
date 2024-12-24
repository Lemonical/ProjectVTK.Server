using ProjectVTK.Shared.Models;

namespace ProjectVTK.Server.Core.Models.SceneObjects;

// TODO: Folder group name
public class Character(ushort id, string folderGroupName, string folderName) : CharacterBase(id, folderName, false)
{
    public string FolderGroupName { get; set; } = folderGroupName;


}