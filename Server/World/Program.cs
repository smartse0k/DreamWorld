using DreamWorld.World.Data;
using DreamWorld.World.Network;

namespace DreamWorld.World;

/// <summary>
/// 월드 서버 진입점.
/// </summary>
public static class Program
{
    private const int WorldPort = 10001;

    public static async Task Main(string[] args)
    {
        Console.Title = $"DreamWorld - World Server (:{WorldPort})";
        await RunServerAsync();
    }

    public static async Task RunServerAsync(CancellationToken cancellationToken = default)
    {
        // 1. 리소스 로딩
        var dataManager = new GameDataManager();
        dataManager.LoadAll();

        // 2. 월드 매니저 초기화 및 20 TPS 루프 시작
        var worldManager = new WorldManager();
        worldManager.Initialize(dataManager);
        _ = worldManager.RunTickLoopAsync(cancellationToken);

        // 3. 클라이언트 수용 소켓 서버 리스닝 시작
        var networkServer = new WorldNetworkServer(WorldPort, worldManager);
        await networkServer.ListenAsync(cancellationToken);
    }
}
