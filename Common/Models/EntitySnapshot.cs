using System.IO;

namespace DreamWorld.Common.Models
{
    /// <summary>
    /// 월드 내 플레이어 또는 몬스터의 현재 스냅샷 상태
    /// </summary>
    public class EntitySnapshot
    {
        public int EntityId { get; set; }
        public bool IsMonster { get; set; }
        public string Name { get; set; } = string.Empty;
        public float PosX { get; set; }
        public float PosY { get; set; }
        public Direction LookDir { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }

        public void WriteTo(BinaryWriter w)
        {
            w.Write(EntityId);
            w.Write(IsMonster);
            w.Write(Name ?? string.Empty);
            w.Write(PosX);
            w.Write(PosY);
            w.Write((byte)LookDir);
            w.Write(Hp);
            w.Write(MaxHp);
        }

        public static EntitySnapshot ReadFrom(BinaryReader r)
        {
            return new EntitySnapshot
            {
                EntityId = r.ReadInt32(),
                IsMonster = r.ReadBoolean(),
                Name = r.ReadString(),
                PosX = r.ReadSingle(),
                PosY = r.ReadSingle(),
                LookDir = (Direction)r.ReadByte(),
                Hp = r.ReadInt32(),
                MaxHp = r.ReadInt32()
            };
        }
    }
}
