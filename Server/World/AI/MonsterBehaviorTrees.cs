using DreamWorld.Common;
using DreamWorld.World.Entity;

namespace DreamWorld.World.AI;

/// <summary>
/// 몬스터 종류별 공유 Behavior Tree 인스턴스와 AI 설정을 제공하는 클래스.
/// </summary>
public static class MonsterBehaviorTrees
{
    /// <summary>
    /// 아무 행동도 하지 않는 허수아비용 Behavior Tree.
    /// </summary>
    public static BtNode<MonsterAiContext> DummyTree { get; } = CreateDummyTree();

    /// <summary>
    /// 평상시 배회하다가 피격 시 추격 및 반격하는 비선공 몬스터용 Behavior Tree.
    /// </summary>
    public static BtNode<MonsterAiContext> PassiveMeleeTree { get; } = CreatePassiveMeleeTree();

    /// <summary>
    /// 인식 범위 내 플레이어를 먼저 감지하여 추격 및 공격하는 선공 몬스터용 Behavior Tree.
    /// </summary>
    public static BtNode<MonsterAiContext> AggressiveMeleeTree { get; } = CreateAggressiveMeleeTree();

    /// <summary>
    /// 몬스터 템플릿 ID에 맞춰 공유 Behavior Tree와 AI 파라미터를 설정합니다.
    /// </summary>
    public static void ConfigureMonster(Monster monster, long nowTick)
    {
        switch (monster.TemplateId)
        {
            // 고대 허수아비 (고정형 더미)
            case 103:
            {
                monster.BehaviorTree = DummyTree;
                monster.IsAggressive = false;
                monster.MoveSpeed = 0f;
                monster.WanderRadius = 0f;
                monster.AttackDamage = 0;
                break;
            }

            // 그림자 늑대, 달빛 골렘 (선공 몬스터)
            case 201:
            case 203:
            {
                monster.BehaviorTree = AggressiveMeleeTree;
                monster.IsAggressive = true;
                monster.MoveSpeed = 2.4f;
                monster.AggroRange = 3.8f;
                monster.AttackRange = 1.25f;
                monster.LeashRange = 7.5f;
                monster.WanderRadius = 2.5f;
                monster.AttackDamage = 12;
                monster.AttackCooldownMs = 1500;
                break;
            }

            // 꿈결 슬라임, 악몽 버섯, 밤안개 정령 등 (비선공 몬스터)
            default:
            {
                monster.BehaviorTree = PassiveMeleeTree;
                monster.IsAggressive = false;
                monster.MoveSpeed = 1.8f;
                monster.AggroRange = 0f;
                monster.AttackRange = 1.2f;
                monster.LeashRange = 7.0f;
                monster.WanderRadius = 2.0f;
                monster.AttackDamage = 8;
                monster.AttackCooldownMs = 1600;
                break;
            }
        }

        monster.ResetAiState(nowTick + 3500);
    }

    private static BtNode<MonsterAiContext> CreateDummyTree()
    {
        return new ActionNode<MonsterAiContext>(IdleAction);
    }

    private static BtNode<MonsterAiContext> CreatePassiveMeleeTree()
    {
        return new SelectorNode<MonsterAiContext>(
            CreateReturnBranch(),
            CreateCombatBranch(),
            new ActionNode<MonsterAiContext>(WanderAction)
        );
    }

    private static BtNode<MonsterAiContext> CreateAggressiveMeleeTree()
    {
        return new SelectorNode<MonsterAiContext>(
            CreateReturnBranch(),
            new SequenceNode<MonsterAiContext>(
                new ConditionNode<MonsterAiContext>(HasNoTargetCondition),
                new ActionNode<MonsterAiContext>(FindAggroTargetAction)
            ),
            CreateCombatBranch(),
            new ActionNode<MonsterAiContext>(WanderAction)
        );
    }

    private static BtNode<MonsterAiContext> CreateReturnBranch()
    {
        return new SequenceNode<MonsterAiContext>(
            new ConditionNode<MonsterAiContext>(ShouldReturnCondition),
            new ActionNode<MonsterAiContext>(ReturnToSpawnAction)
        );
    }

    private static BtNode<MonsterAiContext> CreateCombatBranch()
    {
        return new SequenceNode<MonsterAiContext>(
            new ConditionNode<MonsterAiContext>(HasValidTargetCondition),
            new SelectorNode<MonsterAiContext>(
                new SequenceNode<MonsterAiContext>(
                    new ConditionNode<MonsterAiContext>(IsTargetInAttackRangeCondition),
                    new ActionNode<MonsterAiContext>(AttackTargetAction)
                ),
                new ActionNode<MonsterAiContext>(ChaseTargetAction)
            )
        );
    }

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
        if (!self.IsAggressive || self.AggroRange <= 0f)
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
