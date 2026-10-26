using System.IO;

namespace DreamWorld.Common.Models
{
    /// <summary>
    /// 공격/스킬 적중 결과 정보
    /// </summary>
    public class AttackHitInfo
    {
        public int TargetEntityId { get; set; }
        public int Damage { get; set; }
        public int RemainingHp { get; set; }
        public bool IsDead { get; set; }

        public void WriteTo(BinaryWriter w)
        {
            w.Write(TargetEntityId);
            w.Write(Damage);
            w.Write(RemainingHp);
            w.Write(IsDead);
        }

        public static AttackHitInfo ReadFrom(BinaryReader r)
        {
            return new AttackHitInfo
            {
                TargetEntityId = r.ReadInt32(),
                Damage = r.ReadInt32(),
                RemainingHp = r.ReadInt32(),
                IsDead = r.ReadBoolean()
            };
        }
    }
}
