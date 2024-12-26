namespace ProjectVTK.Server.CLI;

public class ServerState
{
    public bool HasStarted { get; private set; }

    public void StartServer()
        => HasStarted = true;

    public void StopServer()
        => HasStarted = false;
}
