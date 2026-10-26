using System.IO;
using DreamWorld.Common.Models;

namespace DreamWorld.Common.Protocol
{
    public class C_LoginRequestPacket : IPacket
    {
        public PacketId PacketId => PacketId.C_LoginRequest;

        public string AccountId { get; set; } = string.Empty;

        public void Write(BinaryWriter w)
        {
            w.Write(AccountId ?? "guest");
        }

        public void Read(BinaryReader r)
        {
            AccountId = r.ReadString();
        }
    }

    public class S_LoginResponsePacket : IPacket
    {
        public PacketId PacketId => PacketId.S_LoginResponse;

        public bool Success { get; set; }
        public string AuthToken { get; set; } = string.Empty;
        public string WorldIp { get; set; } = "127.0.0.1";
        public int WorldPort { get; set; }
        public CharacterSummary Character { get; set; } = new CharacterSummary();

        public void Write(BinaryWriter w)
        {
            w.Write(Success);
            w.Write(AuthToken ?? string.Empty);
            w.Write(WorldIp ?? "127.0.0.1");
            w.Write(WorldPort);
            Character.WriteTo(w);
        }

        public void Read(BinaryReader r)
        {
            Success = r.ReadBoolean();
            AuthToken = r.ReadString();
            WorldIp = r.ReadString();
            WorldPort = r.ReadInt32();
            Character = CharacterSummary.ReadFrom(r);
        }
    }
}
