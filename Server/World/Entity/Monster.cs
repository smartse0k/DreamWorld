using DreamWorld.World.AI;

namespace DreamWorld.World.Entity;

/// <summary>
/// 몬스터 엔티티.
/// </summary>
public class Monster : Entity
{
    public int TemplateId { get; init; }
    public float SpawnX { get; init; }
    public float SpawnY { get; init; }
    public long RespawnAtTick { get; set; }

    /// <summary>
    /// 몬스터가 공유하는 Behavior Tree 루트 노드.
    /// </summary>
    public BtNode<MonsterAiContext>? BehaviorTree { get; set; }

    public bool IsAggressive { get; set; }
    public float MoveSpeed { get; set; } = 2.0f;
    public float AggroRange { get; set; } = 4.0f;
    public float AttackRange { get; set; } = 1.2f;
    public float LeashRange { get; set; } = 8.0f;
    public float WanderRadius { get; set; } = 2.5f;
    public int AttackDamage { get; set; } = 8;
    public int AttackCooldownMs { get; set; } = 1500;

    /// <summary>
    /// 현재 추격 및 공격 대상인 플레이어의 EntityId.
    /// </summary>
    public int? TargetPlayerId { get; set; }

    /// <summary>
    /// 스폰 위치로 복귀 중인지 여부.
    /// </summary>
    public bool IsReturning { get; set; }

    /// <summary>
    /// 배회 이동 목표 X 좌표.
    /// </summary>
    public float? MoveDestX { get; set; }

    /// <summary>
    /// 배회 이동 목표 Y 좌표.
    /// </summary>
    public float? MoveDestY { get; set; }

    /// <summary>
    /// 다음 배회 이동을 시작할 시각.
    /// </summary>
    public long NextWanderTick { get; set; }

    /// <summary>
    /// 다음 공격이 가능한 시각.
    /// </summary>
    public long NextAttackTick { get; set; }

    /// <summary>
    /// 몬스터의 AI 상태를 초기화합니다.
    /// </summary>
    public void ResetAiState(long nextWanderTick)
    {
        TargetPlayerId = null;
        IsReturning = false;
        MoveDestX = null;
        MoveDestY = null;
        NextWanderTick = nextWanderTick;
        NextAttackTick = 0;
    }
}
