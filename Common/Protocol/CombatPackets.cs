using System.Collections.Generic;
using System.IO;
using DreamWorld.Common.Models;

namespace DreamWorld.Common.Protocol
{
    public class C_AttackPacket : IPacket
    {
        public PacketId PacketId => PacketId.C_Attack;

        public int SkillId { get; set; }
        public Direction LookDir { get; set; }
        public int TargetEntityId { get; set; }

        public void Write(BinaryWriter w)
        {
            w.Write(SkillId);
            w.Write((byte)LookDir);
            w.Write(TargetEntityId);
        }

        public void Read(BinaryReader r)
        {
            SkillId = r.ReadInt32();
            LookDir = (Direction)r.ReadByte();
            TargetEntityId = r.ReadInt32();
        }
    }

    public class S_AttackResultPacket : IPacket
    {
        public PacketId PacketId => PacketId.S_AttackResult;

        public int AttackerId { get; set; }
        public int SkillId { get; set; }
        public Direction LookDir { get; set; }
        public List<AttackHitInfo> Hits { get; set; } = new List<AttackHitInfo>();

        public void Write(BinaryWriter w)
        {
            w.Write(AttackerId);
            w.Write(SkillId);
            w.Write((byte)LookDir);
            w.Write(Hits.Count);
            foreach (var hit in Hits)
            {
                hit.WriteTo(w);
            }
        }

        public void Read(BinaryReader r)
        {
            AttackerId = r.ReadInt32();
            SkillId = r.ReadInt32();
            LookDir = (Direction)r.ReadByte();
            int count = r.ReadInt32();
            Hits = new List<AttackHitInfo>(count);
            for (int i = 0; i < count; i++)
            {
                Hits.Add(AttackHitInfo.ReadFrom(r));
            }
        }
    }
}
