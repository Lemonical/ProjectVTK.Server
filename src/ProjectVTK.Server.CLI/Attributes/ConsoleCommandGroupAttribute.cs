namespace ProjectVTK.Server.CLI.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ConsoleCommandGroupAttribute : Attribute
{
    public string? Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? HeaderName { get; set; }
}

