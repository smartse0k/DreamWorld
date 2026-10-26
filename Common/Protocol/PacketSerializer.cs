using System.IO;
using System.Text;

namespace DreamWorld.Common.Protocol
{
    /// <summary>
    /// 네트워크 패킷을 직렬화 및 역직렬화하는 유틸리티.
    /// </summary>
    public static class PacketSerializer
    {
        /// <summary>
        /// 패킷 객체를 전송용 바이트 배열로 직렬화합니다.
        /// </summary>
        public static byte[] Serialize(IPacket packet)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            // 1. 길이 헤더 자리 예약 (2 bytes)
            writer.Write((ushort)0);
            // 2. PacketId 기록 (2 bytes)
            writer.Write((ushort)packet.PacketId);
            // 3. 패킷별 페이로드 기록
            packet.Write(writer);

            byte[] buffer = ms.ToArray();
            ushort bodyLength = (ushort)(buffer.Length - 2);
            buffer[0] = (byte)(bodyLength & 0xFF);
            buffer[1] = (byte)((bodyLength >> 8) & 0xFF);
            return buffer;
        }

        /// <summary>
        /// BinaryReader 스트림에서 패킷을 역직렬화합니다.
        /// </summary>
        public static T Deserialize<T>(BinaryReader reader) where T : IPacket, new()
        {
            var packet = new T();
            packet.Read(reader);
            return packet;
        }

        /// <summary>
        /// 바이트 배열에서 패킷을 역직렬화합니다.
        /// </summary>
        public static T Deserialize<T>(byte[] payloadBytes) where T : IPacket, new()
        {
            using var ms = new MemoryStream(payloadBytes);
            using var reader = new BinaryReader(ms, Encoding.UTF8);
            return Deserialize<T>(reader);
        }
    }
}
