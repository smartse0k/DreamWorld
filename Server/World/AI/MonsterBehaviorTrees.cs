using DreamWorld.Common;
using DreamWorld.World.Data;
using DreamWorld.World.Entity;

namespace DreamWorld.World.AI;

/// <summary>
/// JSON에서 역직렬화되는 Behavior Tree 노드 설정 데이터.
/// </summary>
public class BtNodeConfig
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<BtNodeConfig> Children { get; set; } = new();
}

/// <summary>
/// JSON 설정으로부터 공유 Behavior Tree를 조립하고 몬스터 AI 동작을 제공하는 클래스.
/// </summary>
public static class MonsterBehaviorTrees
{
    /// <summary>
    /// 기본 폴백용 더미 Behavior Tree.
    /// </summary>
    public static BtNode<MonsterAiContext> DummyTree { get; } = new ActionNode<MonsterAiContext>(IdleAction);

    /// <summary>
    /// JSON 노드 설정 객체로부터 실행 가능한 Behavior Tree 노드를 재귀적으로 생성합니다.
    /// </summary>
    public static BtNode<MonsterAiContext> BuildFromConfig(BtNodeConfig config)
    {
        if (config.Type == "Selector")
        {
            var children = new BtNode<MonsterAiContext>[config.Children.Count];
            for (int i = 0; i < config.Children.Count; i++)
            {
                children[i] = BuildFromConfig(config.Children[i]);
            }
            return new SelectorNode<MonsterAiContext>(children);
        }

        if (config.Type == "Sequence")
        {
            var children = new BtNode<MonsterAiContext>[config.Children.Count];
            for (int i = 0; i < config.Children.Count; i++)
            {
                children[i] = BuildFromConfig(config.Children[i]);
            }
            return new SequenceNode<MonsterAiContext>(children);
        }

        if (config.Type == "Condition")
        {
            return new ConditionNode<MonsterAiContext>(ResolveCondition(config.Name));
        }

        if (config.Type == "Action")
        {
            return new ActionNode<MonsterAiContext>(ResolveAction(config.Name));
        }

        throw new InvalidOperationException($"Unknown BtNode type: '{config.Type}'");
    }

    /// <summary>
    /// 몬스터 정적 데이터와 AI 트리를 몬스터 엔티티에 적용합니다.
    /// </summary>
    public static void ConfigureMonster(
        Monster monster,
        MonsterData spec,
        BtNode<MonsterAiContext>? aiTree,
        long nowTick)
    {
        monster.MaxHp = spec.MaxHp;
        monster.Hp = spec.MaxHp;
        monster.MoveSpeed = spec.MoveSpeed;
        monster.AggroRange = spec.AggroRange;
        monster.AttackRange = spec.AttackRange;
        monster.LeashRange = spec.LeashRange;
        monster.WanderRadius = spec.WanderRadius;
        monster.AttackDamage = spec.AttackDamage;
        monster.AttackCooldownMs = spec.AttackCooldownMs;
        monster.BehaviorTree = aiTree ?? DummyTree;

        monster.ResetAiState(nowTick + 3500);
    }

    private static Func<MonsterAiContext, bool> ResolveCondition(string name)
    {
        switch (name)
        {
            case "ShouldReturn":
            {
                return ShouldReturnCondition;
            }
            case "HasNoTarget":
            {
                return HasNoTargetCondition;
            }
            case "HasValidTarget":
            {
                return HasValidTargetCondition;
            }
            case "IsTargetInAttackRange":
            {
                return IsTargetInAttackRangeCondition;
            }
            default:
            {
                throw new InvalidOperationException($"Unknown BT Condition name: '{name}'");
            }
        }
    }

    private static Func<MonsterAiContext, BtStatus> ResolveAction(string name)
    {
        switch (name)
        {
            case "Idle":
            {
                return IdleAction;
            }
            case "ReturnToSpawn":
            {
                return ReturnToSpawnAction;
            }
            case "FindAggroTarget":
            {
                return FindAggroTargetAction;
            }
            case "AttackTarget":
            {
                return AttackTargetAction;
            }
            case "ChaseTarget":
            {
                return ChaseTargetAction;
            }
            case "Wander":
            {
                return WanderAction;
            }
            default:
            {
                throw new InvalidOperationException($"Unknown BT Action name: '{name}'");
            }
        }
    }

    // ========================================================================
    // Condition & Action 노드 함수들 (무상태 공유)
    // ========================================================================

    private static BtStatus IdleAction(MonsterAiContext ctx)
    {
        return BtStatus.Success;
    }

    private static bool ShouldReturnCondition(MonsterAiContext ctx)
    {
        Monster self = ctx.Self;
        if (self.IsReturning)
        {
            return true;
        }

        if (self.TargetPlayerId.HasValue)
        {
            float distFromSpawn = GridMath.Distance(self.PosX, self.PosY, self.SpawnX, self.SpawnY);
            if (distFromSpawn > self.LeashRange)
            {
                return true;
            }
        }

        return false;
    }

    private static BtStatus ReturnToSpawnAction(MonsterAiContext ctx)
    {
        Monster self = ctx.Self;
        self.TargetPlayerId = null;
        self.MoveDestX = null;
        self.MoveDestY = null;
        self.IsReturning = true;

        float dist = GridMath.Distance(self.PosX, self.PosY, self.SpawnX, self.SpawnY);
        if (dist <= 0.15f)
        {
            self.PosX = self.SpawnX;
            self.PosY = self.SpawnY;
            self.Hp = self.MaxHp;
            self.IsReturning = false;
            self.NextWanderTick = ctx.NowTick + 2500;
            return BtStatus.Success;
        }

        bool moved = ctx.Zone.MoveMonsterToward(self, self.SpawnX, self.SpawnY, ctx.DeltaTime);
        if (!moved)
        {
            // 지형에 막혀 이동할 수 없는 경우 스폰 위치로 즉시 복귀
            ctx.Zone.TeleportMonster(self, self.SpawnX, self.SpawnY);
            self.Hp = self.MaxHp;
            self.IsReturning = false;
            self.NextWanderTick = ctx.NowTick + 2500;
            return BtStatus.Success;
        }

        return BtStatus.Running;
    }

    private static bool HasNoTargetCondition(MonsterAiContext ctx)
    {
        return !ctx.Self.TargetPlayerId.HasValue && !ctx.Self.IsReturning;
    }

    private static BtStatus FindAggroTargetAction(MonsterAiContext ctx)
    {
        Monster self = ctx.Self;
        if (self.AggroRange <= 0f)
        {
            return BtStatus.Failure;
        }

        Player? target = ctx.Zone.FindNearestPlayerInRange(self.PosX, self.PosY, self.AggroRange);
        if (target == null)
        {
            return BtStatus.Failure;
        }

        float targetDistFromSpawn = GridMath.Distance(target.PosX, target.PosY, self.SpawnX, self.SpawnY);
        if (targetDistFromSpawn > self.LeashRange)
        {
            return BtStatus.Failure;
        }

        self.TargetPlayerId = target.EntityId;
        self.MoveDestX = null;
        self.MoveDestY = null;
        return BtStatus.Success;
    }

    private static bool HasValidTargetCondition(MonsterAiContext ctx)
    {
        Monster self = ctx.Self;
        if (!self.TargetPlayerId.HasValue)
        {
            return false;
        }

        if (!ctx.Zone.TryGetPlayer(self.TargetPlayerId.Value, out Player? target) || target == null || target.IsDead)
        {
            self.TargetPlayerId = null;
            return false;
        }

        return true;
    }

    private static bool IsTargetInAttackRangeCondition(MonsterAiContext ctx)
    {
        Monster self = ctx.Self;
        if (!self.TargetPlayerId.HasValue)
        {
            return false;
        }

        if (!ctx.Zone.TryGetPlayer(self.TargetPlayerId.Value, out Player? target) || target == null)
        {
            return false;
        }

        float dist = GridMath.Distance(self.PosX, self.PosY, target.PosX, target.PosY);
        return dist <= self.AttackRange;
    }

    private static BtStatus AttackTargetAction(MonsterAiContext ctx)
    {
        Monster self = ctx.Self;
        if (!self.TargetPlayerId.HasValue || !ctx.Zone.TryGetPlayer(self.TargetPlayerId.Value, out Player? target) || target == null)
        {
            self.TargetPlayerId = null;
            return BtStatus.Failure;
        }

        if (ctx.NowTick >= self.NextAttackTick)
        {
            self.NextAttackTick = ctx.NowTick + self.AttackCooldownMs;
            ctx.Zone.PerformMonsterAttack(self, target);
        }

        return BtStatus.Success;
    }

    private static BtStatus ChaseTargetAction(MonsterAiContext ctx)
    {
        Monster self = ctx.Self;
        if (!self.TargetPlayerId.HasValue || !ctx.Zone.TryGetPlayer(self.TargetPlayerId.Value, out Player? target) || target == null)
        {
            self.TargetPlayerId = null;
            return BtStatus.Failure;
        }

        ctx.Zone.MoveMonsterToward(self, target.PosX, target.PosY, ctx.DeltaTime);
        return BtStatus.Running;
    }

    private static BtStatus WanderAction(MonsterAiContext ctx)
    {
        Monster self = ctx.Self;
        if (self.MoveSpeed <= 0f || self.WanderRadius <= 0f)
        {
            return BtStatus.Success;
        }

        if (self.MoveDestX.HasValue && self.MoveDestY.HasValue)
        {
            float dist = GridMath.Distance(self.PosX, self.PosY, self.MoveDestX.Value, self.MoveDestY.Value);
            if (dist <= 0.15f)
            {
                self.MoveDestX = null;
                self.MoveDestY = null;
                self.NextWanderTick = ctx.NowTick + ctx.Zone.NextRandomInt(2500, 4500);
                return BtStatus.Success;
            }

            bool moved = ctx.Zone.MoveMonsterToward(self, self.MoveDestX.Value, self.MoveDestY.Value, ctx.DeltaTime);
            if (!moved)
            {
                self.MoveDestX = null;
                self.MoveDestY = null;
                self.NextWanderTick = ctx.NowTick + ctx.Zone.NextRandomInt(2000, 3500);
                return BtStatus.Success;
            }

            return BtStatus.Running;
        }

        if (ctx.NowTick < self.NextWanderTick)
        {
            return BtStatus.Success;
        }

        if (ctx.Zone.TryPickWanderDestination(self, out float destX, out float destY))
        {
            self.MoveDestX = destX;
            self.MoveDestY = destY;
            return BtStatus.Running;
        }

        self.NextWanderTick = ctx.NowTick + 2000;
        return BtStatus.Success;
    }
}
