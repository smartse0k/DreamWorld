namespace DreamWorld.World.Data;

/// <summary>
/// 몬스터의 기본 능력치 및 AI 설정을 담는 정적 데이터 클래스.
/// </summary>
public class MonsterData
{
    public string MonsterKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxHp { get; set; } = 50;
    public string AiType { get; set; } = "PassiveMelee";
    public float MoveSpeed { get; set; } = 1.8f;
    public float AggroRange { get; set; } = 0.0f;
    public float AttackRange { get; set; } = 1.2f;
    public float LeashRange { get; set; } = 7.0f;
    public float WanderRadius { get; set; } = 2.0f;
    public int AttackDamage { get; set; } = 8;
    public int AttackCooldownMs { get; set; } = 1600;
}
