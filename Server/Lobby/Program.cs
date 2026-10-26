using DreamWorld.Lobby.Network;

namespace DreamWorld.Lobby;

/// <summary>
/// 로비 서버 진입점.
/// </summary>
public static class Program
{
    private const int LobbyPort = 10000;
    private const string WorldServerIp = "127.0.0.1";
    private const int WorldServerPort = 10001;

    public static async Task Main(string[] args)
    {
        Console.Title = $"DreamWorld - Lobby Server (:{LobbyPort})";
        await RunServerAsync();
    }

    public static async Task RunServerAsync(CancellationToken cancellationToken = default)
    {
        var server = new LobbyNetworkServer(LobbyPort, WorldServerIp, WorldServerPort);
        await server.ListenAsync(cancellationToken);
    }
}
