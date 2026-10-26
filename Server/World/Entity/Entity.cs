using DreamWorld.Common;
using DreamWorld.Common.Models;

namespace DreamWorld.World.Entity;

/// <summary>
/// 월드 내 동적 객체의 최상위 추상 클래스.
/// </summary>
public abstract class Entity
{
    public int EntityId { get; init; }
    public string Name { get; set; } = string.Empty;
    public float PosX { get; set; }
    public float PosY { get; set; }
    public Direction LookDir { get; set; } = Direction.Down;
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public bool IsDead => Hp <= 0;

    public (int tileX, int tileY) CurrentTile => GridMath.WorldToTile(PosX, PosY);

    public EntitySnapshot ToSnapshot(bool isMonster)
    {
        return new EntitySnapshot
        {
            EntityId = EntityId,
            IsMonster = isMonster,
            Name = Name,
            PosX = PosX,
            PosY = PosY,
            LookDir = LookDir,
            Hp = Hp,
            MaxHp = MaxHp
        };
    }
}
