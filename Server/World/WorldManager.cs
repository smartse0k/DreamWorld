using DreamWorld.Common;
using DreamWorld.World.Data;
using DreamWorld.World.Entity;

namespace DreamWorld.World;

/// <summary>
/// 다중 지역(Zone)을 관리하는 월드 매니저.
/// </summary>
public class WorldManager
{
    private readonly Dictionary<int, Zone> _zones = new();
    private int _nextEntityId = 1;

    public int ZoneCount => _zones.Count;

    public void Initialize(GameDataManager dataManager)
    {
        _zones.Clear();
        _nextEntityId = 1;

        foreach (var map in dataManager.Maps.Values)
        {
            var zone = new Zone(
                map,
                dataManager.Skills,
                dataManager.Monsters,
                dataManager.AiTrees,
                NextEntityId,
                TransferPlayerViaPortal);
            _zones[map.MapId] = zone;
        }
    }

    public int NextEntityId()
    {
        return Interlocked.Increment(ref _nextEntityId);
    }

    /// <summary>
    /// 월드 업데이트 루프를 주기적으로 실행합니다.
    /// </summary>
    public async Task RunTickLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                foreach (var zone in _zones.Values)
                {
                    zone.UpdateTick();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public Player EnterNewPlayer(int characterId, string characterName, Action<byte[]> sendCallback, int startMapId = 1)
    {
        if (!_zones.TryGetValue(startMapId, out var startZone))
        {
            startZone = _zones.Values.First();
        }

        var player = new Player
        {
            EntityId = NextEntityId(),
            CharacterId = characterId,
            CurrentMapId = startZone.MapId,
            Name = characterName,
            PosX = startZone.Map.SpawnX,
            PosY = startZone.Map.SpawnY,
            LookDir = Direction.Down,
            Hp = 100,
            MaxHp = 100,
            SendBytes = sendCallback
        };

        startZone.EnterPlayer(player, startZone.Map.SpawnX, startZone.Map.SpawnY);
        return player;
    }

    public void RemovePlayer(Player player)
    {
        if (_zones.TryGetValue(player.CurrentMapId, out var zone))
        {
            zone.RemovePlayer(player.EntityId);
        }
    }

    public void HandleMove(Player player, float posX, float posY, Direction lookDir)
    {
        if (_zones.TryGetValue(player.CurrentMapId, out var zone))
        {
            zone.HandleMove(player, posX, posY, lookDir);
        }
    }

    public void HandleAttack(Player player, int skillId, Direction lookDir, int targetEntityId)
    {
        if (_zones.TryGetValue(player.CurrentMapId, out var zone))
        {
            zone.HandleAttack(player, skillId, lookDir, targetEntityId);
        }
    }

    private void TransferPlayerViaPortal(Player player, PortalConfig portal)
    {
        if (!_zones.TryGetValue(portal.TargetMapId, out var targetZone))
        {
            Console.WriteLine($"[WorldManager] Portal target zone {portal.TargetMapId} not found!");
            return;
        }

        player.CurrentMapId = targetZone.MapId;

        Console.WriteLine(
            $"[WorldManager] Portal Triggered! [{player.Name}] warped via '{portal.Name}' -> " +
            $"Zone {portal.TargetMapId} ({targetZone.MapName}) at ({portal.TargetSpawnX:F1}, {portal.TargetSpawnY:F1})");

        targetZone.EnterPlayer(player, portal.TargetSpawnX, portal.TargetSpawnY);
    }
}
