using DreamWorld.Server.Core.Network;

namespace DreamWorld.World.Network;

/// <summary>
/// 월드 서버의 클라이언트 연결을 수용하는 네트워크 서버.
/// </summary>
public class WorldNetworkServer
{
    private readonly TcpServerHost _host;

    public WorldNetworkServer(int port, WorldManager worldManager)
    {
        _host = new TcpServerHost(
            serverName: $"WorldServer (Zones:{worldManager.ZoneCount})",
            port: port,
            sessionFactory: () => new WorldClientSession(worldManager));
    }

    public Task ListenAsync(CancellationToken cancellationToken = default)
    {
        return _host.ListenAsync(cancellationToken);
    }
}
