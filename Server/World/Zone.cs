using DreamWorld.Common;
using DreamWorld.Common.Models;
using DreamWorld.Common.Protocol;
using DreamWorld.Server.Core;
using DreamWorld.World.AI;
using DreamWorld.World.Data;
using DreamWorld.World.Entity;

namespace DreamWorld.World;

/// <summary>
/// 단일 맵 내의 엔티티와 게임 로직을 관리하는 지역 단위.
/// </summary>
public class Zone
{
    private const float TickDeltaSeconds = 0.05f;

    private readonly JobQueue _jobQueue = new();
    private readonly Random _rng = new();
    private readonly GameMap _map;
    private readonly IReadOnlyDictionary<int, SkillData> _skills;
    private readonly IReadOnlyDictionary<string, MonsterData> _monsterSpecs;
    private readonly Func<int> _entityIdGenerator;
    private readonly Action<Player, PortalConfig>? _onPortalTouched;

    private readonly Dictionary<int, Player> _players = new();
    private readonly Dictionary<int, Monster> _monsters = new();

    public int MapId => _map.MapId;
    public string MapName => _map.MapName;
    public GameMap Map => _map;

    public Zone(
        GameMap map,
        IReadOnlyDictionary<int, SkillData> skills,
        IReadOnlyDictionary<string, MonsterData> monsterSpecs,
        Func<int> entityIdGenerator,
        Action<Player, PortalConfig>? onPortalTouched = null)
    {
        _map = map;
        _skills = skills;
        _monsterSpecs = monsterSpecs;
        _entityIdGenerator = entityIdGenerator;
        _onPortalTouched = onPortalTouched;
        SpawnInitialMonsters();
    }

    private void SpawnInitialMonsters()
    {
        long now = Environment.TickCount64;
        foreach (var spawn in _map.MonsterSpawns)
        {
            if (!_monsterSpecs.TryGetValue(spawn.MonsterKey, out MonsterData? spec) || spec == null)
            {
                spec = new MonsterData
                {
                    MonsterKey = spawn.MonsterKey,
                    Name = spawn.MonsterKey
                };
            }

            int id = _entityIdGenerator();
            var (wx, wy) = GridMath.TileToWorldCenter(spawn.X, spawn.Y);
            var monster = new Monster
            {
                EntityId = id,
                MonsterKey = spec.MonsterKey,
                Name = spec.Name,
                PosX = wx,
                PosY = wy,
                SpawnX = wx,
                SpawnY = wy,
                LookDir = Direction.Down,
                RespawnMs = spawn.RespawnMs > 0 ? spawn.RespawnMs : 5000
            };

            MonsterBehaviorTrees.ConfigureMonster(monster, spec, now + _rng.Next(0, 1500));
            _monsters[id] = monster;

            Console.WriteLine($"[Zone:Map{_map.MapId}] Monster Spawned: [{id}] {monster.MonsterKey} ('{monster.Name}') at Tile({spawn.X}, {spawn.Y}) HP={monster.MaxHp} Respawn={monster.RespawnMs}ms");
        }
    }

    public void EnterPlayer(Player player, float spawnX, float spawnY)
    {
        _jobQueue.Enqueue(() => ProcessEnterPlayer(player, spawnX, spawnY));
    }

    public void RemovePlayer(int entityId)
    {
        _jobQueue.Enqueue(() => ProcessRemovePlayer(entityId));
    }

    public void HandleMove(Player player, float targetX, float targetY, Direction lookDir)
    {
        _jobQueue.Enqueue(() => ProcessMove(player, targetX, targetY, lookDir));
    }

    public void HandleAttack(Player attacker, int skillId, Direction lookDir, int targetEntityId)
    {
        _jobQueue.Enqueue(() => ProcessAttack(attacker, skillId, lookDir, targetEntityId));
    }

    /// <summary>
    /// 대기 중인 작업을 처리하고 지역 상태를 갱신합니다.
    /// </summary>
    public void UpdateTick()
    {
        // 1. 이번 틱까지 큐에 쌓인 모든 플레이어 입퇴장/이동/공격 작업 일괄 처리
        _jobQueue.Flush();

        // 2. 몬스터 리스폰 및 Behavior Tree AI 업데이트
        long now = Environment.TickCount64;
        foreach (var monster in _monsters.Values)
        {
            if (monster.IsDead)
            {
                if (monster.RespawnAtTick > 0 && now >= monster.RespawnAtTick)
                {
                    monster.Hp = monster.MaxHp;
                    monster.PosX = monster.SpawnX;
                    monster.PosY = monster.SpawnY;
                    monster.LookDir = Direction.Down;
                    monster.RespawnAtTick = 0;
                    monster.ResetAiState(now + 2500);

                    Console.WriteLine($"[Zone:Map{_map.MapId}] Monster Respawned: [{monster.EntityId}] {monster.Name} HP={monster.Hp}");
                    Broadcast(PacketSerializer.Serialize(new S_MoveBroadcastPacket
                    {
                        EntityId = monster.EntityId,
                        PosX = monster.PosX,
                        PosY = monster.PosY,
                        LookDir = monster.LookDir
                    }));
                }
                continue;
            }

            if (_players.Count > 0 && monster.BehaviorTree != null)
            {
                var context = new MonsterAiContext(monster, this, now, TickDeltaSeconds);
                monster.BehaviorTree.Tick(context);
            }
        }
    }

    /// <summary>
    /// 지정된 범위의 난수를 반환합니다.
    /// </summary>
    public int NextRandomInt(int minInclusive, int maxExclusive)
    {
        return _rng.Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// 지역 내에 접속 중인 플레이어를 조회합니다.
    /// </summary>
    public bool TryGetPlayer(int entityId, out Player? player)
    {
        return _players.TryGetValue(entityId, out player);
    }

    /// <summary>
    /// 기준 좌표에서 지정된 반경 내에 있는 가장 가까운 생존 플레이어를 찾습니다.
    /// </summary>
    public Player? FindNearestPlayerInRange(float centerX, float centerY, float maxRange)
    {
        Player? nearest = null;
        float bestDist = maxRange;

        foreach (var player in _players.Values)
        {
            if (player.IsDead)
            {
                continue;
            }

            float dist = GridMath.Distance(centerX, centerY, player.PosX, player.PosY);
            if (dist <= bestDist)
            {
                bestDist = dist;
                nearest = player;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 몬스터 스폰 위치 주변에서 이동 가능한 배회 목적지를 선택합니다.
    /// </summary>
    public bool TryPickWanderDestination(Monster monster, out float destX, out float destY)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            float radius = (float)(_rng.NextDouble() * monster.WanderRadius);
            float candidateX = monster.SpawnX + (float)Math.Cos(angle) * radius;
            float candidateY = monster.SpawnY + (float)Math.Sin(angle) * radius;

            if (_map.CanStandAt(candidateX, candidateY))
            {
                destX = candidateX;
                destY = candidateY;
                return true;
            }
        }

        destX = monster.SpawnX;
        destY = monster.SpawnY;
        return false;
    }

    /// <summary>
    /// 몬스터를 목표 좌표 방향으로 한 틱만큼 이동시키고 결과를 브로드캐스트합니다.
    /// </summary>
    public bool MoveMonsterToward(Monster monster, float targetX, float targetY, float deltaTime)
    {
        float dx = targetX - monster.PosX;
        float dy = targetY - monster.PosY;
        float dist = GridMath.Distance(monster.PosX, monster.PosY, targetX, targetY);
        if (dist <= 0.01f)
        {
            return true;
        }

        monster.LookDir = GridMath.VectorToDirection(dx, dy, monster.LookDir);

        float maxStep = monster.MoveSpeed * deltaTime;
        float ratio = dist <= maxStep ? 1.0f : (maxStep / dist);
        float stepX = dx * ratio;
        float stepY = dy * ratio;

        float nextX = monster.PosX;
        float nextY = monster.PosY;
        bool moved = false;

        if (_map.CanStandAt(monster.PosX + stepX, nextY))
        {
            nextX = monster.PosX + stepX;
            moved = true;
        }

        if (_map.CanStandAt(nextX, monster.PosY + stepY))
        {
            nextY = monster.PosY + stepY;
            moved = true;
        }

        if (!moved)
        {
            return false;
        }

        monster.PosX = nextX;
        monster.PosY = nextY;

        Broadcast(PacketSerializer.Serialize(new S_MoveBroadcastPacket
        {
            EntityId = monster.EntityId,
            PosX = monster.PosX,
            PosY = monster.PosY,
            LookDir = monster.LookDir
        }));

        return true;
    }

    /// <summary>
    /// 몬스터를 지정된 좌표로 즉시 이동시키고 브로드캐스트합니다.
    /// </summary>
    public void TeleportMonster(Monster monster, float x, float y)
    {
        monster.PosX = x;
        monster.PosY = y;

        Broadcast(PacketSerializer.Serialize(new S_MoveBroadcastPacket
        {
            EntityId = monster.EntityId,
            PosX = monster.PosX,
            PosY = monster.PosY,
            LookDir = monster.LookDir
        }));
    }

    /// <summary>
    /// 몬스터가 대상 플레이어를 공격하고 결과를 브로드캐스트합니다.
    /// </summary>
    public void PerformMonsterAttack(Monster monster, Player target)
    {
        float dx = target.PosX - monster.PosX;
        float dy = target.PosY - monster.PosY;
        monster.LookDir = GridMath.VectorToDirection(dx, dy, monster.LookDir);

        target.Hp = Math.Max(1, target.Hp - monster.AttackDamage);

        var hitInfo = new AttackHitInfo
        {
            TargetEntityId = target.EntityId,
            Damage = monster.AttackDamage,
            RemainingHp = target.Hp,
            IsDead = false
        };

        Console.WriteLine(
            $"[Zone:Map{_map.MapId}] Monster Attack -> [{monster.Name}#{monster.EntityId}] hit [{target.Name}#{target.EntityId}] " +
            $"| Dmg:{monster.AttackDamage}, PlayerHP:{target.Hp}/{target.MaxHp}");

        Broadcast(PacketSerializer.Serialize(new S_AttackResultPacket
        {
            AttackerId = monster.EntityId,
            SkillId = 1,
            LookDir = monster.LookDir,
            Hits = new List<AttackHitInfo> { hitInfo }
        }));
    }

    private void ProcessEnterPlayer(Player player, float spawnX, float spawnY)
    {
        player.CurrentMapId = _map.MapId;
        player.PosX = spawnX;
        player.PosY = spawnY;
        _players[player.EntityId] = player;

        var snapshots = new List<EntitySnapshot>();
        foreach (var p in _players.Values)
        {
            snapshots.Add(p.ToSnapshot(isMonster: false));
        }
        foreach (var m in _monsters.Values.Where(m => !m.IsDead))
        {
            snapshots.Add(m.ToSnapshot(isMonster: true));
        }

        // 1. 본인에게 맵 지형(타일/포탈) + 스폰 위치 + 엔티티 목록 전송
        player.SendPacket(new S_EnterWorldSuccessPacket
        {
            MyEntityId = player.EntityId,
            MapId = _map.MapId,
            MapName = _map.MapName,
            MapWidth = _map.Width,
            MapHeight = _map.Height,
            MapTiles = _map.GetFlattenedTiles(),
            SpawnX = player.PosX,
            SpawnY = player.PosY,
            Entities = snapshots
        });

        // 2. 같은 Zone의 기존 유저들에게 진입 브로드캐스트
        Broadcast(PacketSerializer.Serialize(new S_MoveBroadcastPacket
        {
            EntityId = player.EntityId,
            PosX = player.PosX,
            PosY = player.PosY,
            LookDir = player.LookDir
        }), excludeEntityId: player.EntityId);

        Console.WriteLine($"[Zone:Map{_map.MapId}] Player Entered: [{player.EntityId}] {player.Name} at ({player.PosX:F1}, {player.PosY:F1})");
    }

    private void ProcessRemovePlayer(int entityId)
    {
        if (_players.Remove(entityId, out var player))
        {
            Broadcast(PacketSerializer.Serialize(new S_EntityDespawnPacket
            {
                EntityId = entityId
            }), excludeEntityId: entityId);

            Console.WriteLine($"[Zone:Map{_map.MapId}] Player Left: [{entityId}] {player.Name}");
        }
    }

    private void ProcessMove(Player player, float targetX, float targetY, Direction lookDir)
    {
        if (!_players.ContainsKey(player.EntityId))
        {
            return;
        }

        player.LookDir = lookDir;

        if (_map.CanStandAt(targetX, targetY))
        {
            player.PosX = targetX;
            player.PosY = targetY;

            long now = Environment.TickCount64;
            if (now >= player.PortalCooldownUntil &&
                _map.TryFindTouchedPortal(player.PosX, player.PosY, out var portal))
            {
                player.PortalCooldownUntil = now + 800;
                // 현재 Zone에서 즉시 퇴장 처리 후 목적지 Zone으로 이동 요청
                ProcessRemovePlayer(player.EntityId);
                _onPortalTouched?.Invoke(player, portal);
                return;
            }
        }
        else
        {
            Console.WriteLine($"[Zone:Map{_map.MapId}] Move blocked by tile collision for [{player.EntityId}] at ({targetX:F2}, {targetY:F2})");
        }

        Broadcast(PacketSerializer.Serialize(new S_MoveBroadcastPacket
        {
            EntityId = player.EntityId,
            PosX = player.PosX,
            PosY = player.PosY,
            LookDir = player.LookDir
        }));
    }

    private void ProcessAttack(Player attacker, int skillId, Direction lookDir, int targetEntityId)
    {
        if (!_players.ContainsKey(attacker.EntityId))
        {
            return;
        }

        attacker.LookDir = lookDir;

        if (!_skills.TryGetValue(skillId, out var skill))
        {
            skill = _skills[1];
        }

        long now = Environment.TickCount64;
        if (attacker.SkillCooldownUntil.TryGetValue(skill.SkillId, out long readyAt) && now < readyAt)
        {
            return;
        }
        attacker.SkillCooldownUntil[skill.SkillId] = now + skill.CooldownMs;

        var hitTargets = new List<Monster>();

        if (skill.TargetingType == AttackTargetingType.FrontTile)
        {
            var frontTiles = GridMath.GetFrontTargetTiles(
                attacker.PosX,
                attacker.PosY,
                lookDir,
                skill.TileRange,
                _map.IsTileWall);

            var candidates = new List<(Monster monster, float dist)>();
            foreach (var monster in _monsters.Values)
            {
                if (monster.IsDead)
                {
                    continue;
                }

                var (mTileX, mTileY) = monster.CurrentTile;
                bool isHit = false;

                foreach (var (ftx, fty) in frontTiles)
                {
                    if (mTileX == ftx && mTileY == fty)
                    {
                        isHit = true;
                        break;
                    }

                    var (centerX, centerY) = GridMath.TileToWorldCenter(ftx, fty);
                    if (GridMath.Distance(monster.PosX, monster.PosY, centerX, centerY) <= GridMath.FrontTileHitTolerance)
                    {
                        isHit = true;
                        break;
                    }
                }

                if (isHit)
                {
                    float dist = GridMath.Distance(attacker.PosX, attacker.PosY, monster.PosX, monster.PosY);
                    candidates.Add((monster, dist));
                }
            }

            hitTargets.AddRange(candidates
                .OrderBy(c => c.dist)
                .Take(skill.MaxTargets)
                .Select(c => c.monster));
        }
        else if (skill.TargetingType == AttackTargetingType.SingleTarget)
        {
            if (_monsters.TryGetValue(targetEntityId, out var monster) && !monster.IsDead)
            {
                float dist = GridMath.Distance(attacker.PosX, attacker.PosY, monster.PosX, monster.PosY);
                if (dist <= skill.WorldRange)
                {
                    hitTargets.Add(monster);
                }
            }
        }

        var hitResults = new List<AttackHitInfo>(hitTargets.Count);
        foreach (var monster in hitTargets)
        {
            monster.Hp = Math.Max(0, monster.Hp - skill.Damage);
            bool died = monster.IsDead;
            if (died)
            {
                monster.RespawnAtTick = now + monster.RespawnMs;
                monster.ResetAiState(0);
            }
            else if (monster.MoveSpeed > 0f)
            {
                // 생존한 몬스터는 공격한 플레이어를 타겟으로 지정 (비선공 몬스터의 반격/추격 활성화)
                monster.TargetPlayerId = attacker.EntityId;
                monster.IsReturning = false;
                monster.MoveDestX = null;
                monster.MoveDestY = null;
                if (monster.NextAttackTick < now + 500)
                {
                    monster.NextAttackTick = now + 500;
                }
            }

            hitResults.Add(new AttackHitInfo
            {
                TargetEntityId = monster.EntityId,
                Damage = skill.Damage,
                RemainingHp = monster.Hp,
                IsDead = died
            });

            Console.WriteLine(
                $"[Zone:Map{_map.MapId}] Combat -> [{attacker.Name}] used '{skill.Name}' (Dir:{lookDir}) " +
                $"on [{monster.Name}#{monster.EntityId}] | Dmg:{skill.Damage}, HP:{monster.Hp}/{monster.MaxHp} {(died ? "(DEAD)" : "")}");
        }

        if (hitResults.Count == 0)
        {
            var (tx, ty) = attacker.CurrentTile;
            var (dx, dy) = GridMath.GetDirectionOffset(lookDir);
            Console.WriteLine(
                $"[Zone:Map{_map.MapId}] Combat -> [{attacker.Name}] swung '{skill.Name}' (Dir:{lookDir}) " +
                $"toward FrontTile({tx + dx}, {ty + dy}) - Miss (No monster)");
        }

        Broadcast(PacketSerializer.Serialize(new S_AttackResultPacket
        {
            AttackerId = attacker.EntityId,
            SkillId = skill.SkillId,
            LookDir = lookDir,
            Hits = hitResults
        }));
    }

    private void Broadcast(byte[] packet, int excludeEntityId = 0)
    {
        foreach (var p in _players.Values)
        {
            if (p.EntityId == excludeEntityId)
            {
                continue;
            }
            p.SendBytes(packet);
        }
    }
}
