using DreamWorld.Common.Protocol;

namespace DreamWorld.World.Entity;

/// <summary>
/// 플레이어 엔티티.
/// </summary>
public class Player : Entity
{
    public int CharacterId { get; init; }
    public int CurrentMapId { get; set; } = 1;

    /// <summary>
    /// 패킷 전송 콜백.
    /// </summary>
    public Action<byte[]> SendBytes { get; init; } = _ => { };

    /// <summary>
    /// 포탈 재사용 가능 시각.
    /// </summary>
    public long PortalCooldownUntil { get; set; }

    /// <summary>
    /// 스킬별 재사용 가능 시각.
    /// </summary>
    public Dictionary<int, long> SkillCooldownUntil { get; } = new();

    public void SendPacket(IPacket packet)
    {
        SendBytes(PacketSerializer.Serialize(packet));
    }
}
