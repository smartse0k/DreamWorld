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

    /// <summary>
    /// 공격 또는 스킬 판정 결과를 브로드캐스트하는 패킷.
    /// </summary>
    public class S_AttackResultPacket : IPacket
    {
        public PacketId PacketId => PacketId.S_AttackResult;

        public int AttackerId { get; set; }
        public int SkillId { get; set; }
        public Direction LookDir { get; set; }
        public int TileRange { get; set; } = 1;
        public bool IsBlockedByWall { get; set; }
        public int BlockedTileX { get; set; }
        public int BlockedTileY { get; set; }
        public List<AttackHitInfo> Hits { get; set; } = new List<AttackHitInfo>();

        public void Write(BinaryWriter w)
        {
            w.Write(AttackerId);
            w.Write(SkillId);
            w.Write((byte)LookDir);
            w.Write(TileRange);
            w.Write(IsBlockedByWall);
            w.Write(BlockedTileX);
            w.Write(BlockedTileY);
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
            TileRange = r.ReadInt32();
            IsBlockedByWall = r.ReadBoolean();
            BlockedTileX = r.ReadInt32();
            BlockedTileY = r.ReadInt32();
            int count = r.ReadInt32();
            Hits = new List<AttackHitInfo>(count);
            for (int i = 0; i < count; i++)
            {
                Hits.Add(AttackHitInfo.ReadFrom(r));
            }
        }
    }
}
