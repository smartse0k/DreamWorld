using System.IO;
using DreamWorld.Common;
using DreamWorld.Common.Protocol;
using DreamWorld.Server.Core.Network;
using DreamWorld.World.Entity;

namespace DreamWorld.World.Network;

/// <summary>
/// 월드 서버에 접속한 클라이언트의 패킷을 처리하는 세션.
/// </summary>
public class WorldClientSession : TcpSession
{
    private readonly WorldManager _worldManager;
    private Player? _player;

    public WorldClientSession(WorldManager worldManager)
    {
        _worldManager = worldManager;
    }

    protected override void OnPacketReceived(PacketId packetId, BinaryReader reader)
    {
        switch (packetId)
        {
            case PacketId.C_EnterWorld:
            {
                var pkt = PacketSerializer.Deserialize<C_EnterWorldPacket>(reader);
                _player = _worldManager.EnterNewPlayer(pkt.CharacterId, pkt.CharacterName, SendRaw);
                break;
            }
            case PacketId.C_Move:
            {
                if (_player == null)
                {
                    break;
                }
                var pkt = PacketSerializer.Deserialize<C_MovePacket>(reader);
                _worldManager.HandleMove(_player, pkt.PosX, pkt.PosY, pkt.LookDir);
                break;
            }
            case PacketId.C_Attack:
            {
                if (_player == null)
                {
                    break;
                }
                var pkt = PacketSerializer.Deserialize<C_AttackPacket>(reader);
                _worldManager.HandleAttack(_player, pkt.SkillId, pkt.LookDir, pkt.TargetEntityId);
                break;
            }
        }
    }

    protected override void OnDisconnected()
    {
        if (_player != null)
        {
            _worldManager.RemovePlayer(_player);
            _player = null;
        }
    }

    protected override void OnError(Exception ex)
    {
        Console.WriteLine($"[WorldClientSession] Error ({RemoteEndPoint}): {ex.Message}");
    }
}
