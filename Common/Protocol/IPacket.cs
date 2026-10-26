using System.IO;

namespace DreamWorld.Common.Protocol
{
    /// <summary>
    /// 네트워크 패킷의 공통 인터페이스.
    /// </summary>
    public interface IPacket
    {
        PacketId PacketId { get; }
        void Write(BinaryWriter writer);
        void Read(BinaryReader reader);
    }
}
