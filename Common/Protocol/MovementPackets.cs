using System.IO;

namespace DreamWorld.Common.Protocol
{
    public class C_MovePacket : IPacket
    {
        public PacketId PacketId => PacketId.C_Move;

        public float PosX { get; set; }
        public float PosY { get; set; }
        public Direction LookDir { get; set; }

        public void Write(BinaryWriter w)
        {
            w.Write(PosX);
            w.Write(PosY);
            w.Write((byte)LookDir);
        }

        public void Read(BinaryReader r)
        {
            PosX = r.ReadSingle();
            PosY = r.ReadSingle();
            LookDir = (Direction)r.ReadByte();
        }
    }

    public class S_MoveBroadcastPacket : IPacket
    {
        public PacketId PacketId => PacketId.S_MoveBroadcast;

        public int EntityId { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public Direction LookDir { get; set; }

        public void Write(BinaryWriter w)
        {
            w.Write(EntityId);
            w.Write(PosX);
            w.Write(PosY);
            w.Write((byte)LookDir);
        }

        public void Read(BinaryReader r)
        {
            EntityId = r.ReadInt32();
            PosX = r.ReadSingle();
            PosY = r.ReadSingle();
            LookDir = (Direction)r.ReadByte();
        }
    }
}
