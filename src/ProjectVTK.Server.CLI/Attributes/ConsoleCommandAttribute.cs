namespace ProjectVTK.Server.CLI.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class ConsoleCommandAttribute(string name, string description, params string[] aliases) : Attribute
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public string[] Aliases { get; } = aliases;
}