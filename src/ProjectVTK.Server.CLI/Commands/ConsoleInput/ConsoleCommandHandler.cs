using Kokuban;
using Microsoft.Extensions.Logging;
using ProjectVTK.Server.CLI.Attributes;
using System.Reflection;

namespace ProjectVTK.Server.CLI.Commands.ConsoleInput;

public class ConsoleCommandHandler
{
    private readonly Dictionary<string, (string headerName, string groupDesc, object instance, MethodInfo? method)> _commands = [];
    private readonly ILogger<ConsoleCommandHandler> _logger;

    public ConsoleCommandHandler(IServiceProvider serviceProvider, ILogger<ConsoleCommandHandler> logger)
    {
        _logger = logger;
        var commandGroups = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<ConsoleCommandGroupAttribute>() != null);

        foreach (var groupType in commandGroups)
        {
            var groupAttr = groupType.GetCustomAttribute<ConsoleCommandGroupAttribute>();
            if (groupAttr == null) continue;

            var constructor = groupType.GetConstructors(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault();
            if (constructor == null) continue;

            var groupName = groupAttr.Name?.ToLower();

            List<object> args = [];
            var parameters = constructor.GetParameters().Select(x => x.ParameterType).ToList();
            foreach (var item in parameters)
            {
                if (item == GetType())
                {
                    args.Add(this);
                    continue;
                }

                var service = serviceProvider.GetService(item);
                if (service != null)
                    args.Add(service);
                else
                    _logger.LogCritical("Failed to get service '{service}'", item.Name);
            };

            if (args.Count != parameters.Count)
            {
                _logger.LogCritical("Failed to create an instance of '{service}'", groupType.Name);
                continue;
            }

            var groupInstance = constructor.Invoke([.. args]);
            //var groupInstance = Activator.CreateInstance(groupType, args);
            if (groupInstance == null) continue;

            var methods = groupType.GetMethods()
                .Where(m => m.GetCustomAttribute<ConsoleCommandAttribute>() != null);

            // Skip if no methods in the command group were found
            if (!methods.Any()) continue;

            foreach (var method in methods)
            {
                var commandAttr = method.GetCustomAttribute<ConsoleCommandAttribute>();
                if (commandAttr == null) continue;

                // Register
                // Command name
                var commandName = $"{groupName} {commandAttr.Name}".ToLower().Trim();
                _commands[commandName] = (groupAttr.HeaderName ?? string.Empty, groupAttr.Description ?? string.Empty, groupInstance, method);

                // Command aliases
                foreach (var alias in commandAttr.Aliases)
                {
                    var commandAlias = $"{groupName} {alias}".ToLower().Trim();
                    _commands[commandAlias] = (groupAttr.HeaderName ?? string.Empty, groupAttr.Description ?? string.Empty, groupInstance, method);
                }
            }
        }
    }

    public async Task ExecuteCommandAsync(string input)
    {
        var args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0) return;

        var commandKey = string.Join(' ', args.Take(2)).ToLower();
        string[] commandArgs = [];
        if (!_commands.TryGetValue(commandKey, out var command))
        {
            // Check if it's a group command without subcommand
            commandKey = args[0].ToLower();
            if (!_commands.TryGetValue(commandKey, out command))
            {
                Console.WriteLine(Chalk.BrightRed[$"Unknown command '{input}'"]);
                return;
            }
            else
                commandArgs = [.. args.Skip(1)];
        }
        else
            commandArgs = [.. args.Skip(2)];


        var (_, _, instance, method) = command;
        if (method != null)
        {
            var parameters = method.GetParameters();
            object? result = null;

            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                result = method.Invoke(instance, [commandArgs]);
            else if (parameters.Length == 0)
                result = method.Invoke(instance, null);

            if (result is Task task)
                await task;
        }
        //else
        //    Console.WriteLine(Chalk.Red[$"No subcommands found for '{args[0]}'"]);
    }

    public IEnumerable<(string HeaderName, string GroupDescription, string Command, string Description)> GetCommands()
        => _commands
            .Where(kvp => kvp.Value.method != null)
            .Select(kvp =>
            {
                var commandKey = kvp.Key;
                var methodInfo = kvp.Value.method;
                var commandAttr = methodInfo!.GetCustomAttribute<ConsoleCommandAttribute>();
                var description = commandAttr?.Description ?? string.Empty;
                return (kvp.Value.headerName, kvp.Value.groupDesc, commandKey, description);
            });
}
