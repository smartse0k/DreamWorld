using DreamWorld.World.Entity;

namespace DreamWorld.World.AI;

/// <summary>
/// 몬스터 Behavior Tree 평가에 필요한 실행 컨텍스트.
/// </summary>
public readonly struct MonsterAiContext
{
    public Monster Self { get; }
    public Zone Zone { get; }
    public long NowTick { get; }
    public float DeltaTime { get; }

    public MonsterAiContext(Monster self, Zone zone, long nowTick, float deltaTime)
    {
        Self = self;
        Zone = zone;
        NowTick = nowTick;
        DeltaTime = deltaTime;
    }
}
