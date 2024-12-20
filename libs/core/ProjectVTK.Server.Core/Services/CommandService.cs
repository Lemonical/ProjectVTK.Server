using Fleck;
using Microsoft.Extensions.Logging;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Helpers;
using System.Collections.Concurrent;
using Websocket.Client;

namespace ProjectVTK.Server.Core.Services;

public class CommandService(ILogger<CommandService> logger)
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<Command>> _pendingCommands = new();
    private readonly ILogger<CommandService> _logger = logger;

    public Task<Command> SendCommandAsync(object socket, Command command, TimeSpan timeout = default)
    {
        if (command.Id is not Guid id || id == default)
            throw new InvalidOperationException($"Command ID {command.Id} is invalid.");

        var tcs = new TaskCompletionSource<Command>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pendingCommands.TryAdd(command.Id.Value, tcs))
            throw new InvalidOperationException($"A command with ID {command.Id} is already awaiting response.");

        try
        {
            if (socket is WebsocketClient clientSocket)
                clientSocket.Send(command.AsJson());
            else if (socket is IWebSocketConnection sessionSocket)
                sessionSocket.Send(command.AsJson());

            if (timeout == default)
                timeout = TimeSpan.FromSeconds(15);

            // Timeout
            Task.Delay(timeout).ContinueWith(_ =>
            {
                if (_pendingCommands.TryRemove(command.Id.Value, out var timedOutTcs))
                {
                    _logger.LogWarning("Command {CommandId} timed out.", command.Id.Value);
                    timedOutTcs.TrySetException(new TimeoutException($"Command {command.Id.Value} timed out."));
                }
            });

            return tcs.Task;
        }
        catch (Exception ex)
        {
            _pendingCommands.TryRemove(command.Id.Value, out _);
            tcs.TrySetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Completes a pending command when a response is received.
    /// </summary>
    public bool TryCompleteCommand(Guid commandId, Command response)
    {
        if (_pendingCommands.TryRemove(commandId, out var tcs))
        {
            tcs.TrySetResult(response);
            return true;
        }
        else if (response.Status != null)
            _logger.LogWarning("No pending command found for Command {id}.", commandId);

        return false;
    }
}
