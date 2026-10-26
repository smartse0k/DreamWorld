using System.IO;
using DreamWorld.Common;
using DreamWorld.Common.Models;
using DreamWorld.Common.Protocol;
using DreamWorld.Server.Core.Network;

namespace DreamWorld.Lobby.Network;

/// <summary>
/// 로비 서버의 패킷을 처리하는 세션.
/// </summary>
public class LobbySession : TcpSession
{
    private readonly LobbyNetworkServer _server;

    public LobbySession(LobbyNetworkServer server)
    {
        _server = server;
    }

    protected override void OnPacketReceived(PacketId packetId, BinaryReader reader)
    {
        switch (packetId)
        {
            case PacketId.C_LoginRequest:
            {
                var req = PacketSerializer.Deserialize<C_LoginRequestPacket>(reader);
                var response = _server.CreateLoginResponse(req.AccountId);
                Send(response);
                break;
            }
        }
    }

    protected override void OnError(Exception ex)
    {
        Console.WriteLine($"[LobbySession] Error ({RemoteEndPoint}): {ex.Message}");
    }
}

/// <summary>
/// 로비 서버의 네트워크 연결과 로그인 요청을 관리하는 서버.
/// </summary>
public class LobbyNetworkServer
{
    private readonly TcpServerHost _host;
    private readonly string _worldServerIp;
    private readonly int _worldServerPort;
    private int _nextCharacterId = 1000;

    public LobbyNetworkServer(int lobbyPort, string defaultWorldServerIp, int defaultWorldServerPort)
    {
        _worldServerIp = defaultWorldServerIp;
        _worldServerPort = defaultWorldServerPort;

        _host = new TcpServerHost(
            serverName: "LobbyServer",
            port: lobbyPort,
            sessionFactory: () => new LobbySession(this));
    }

    public Task ListenAsync(CancellationToken cancellationToken = default)
    {
        return _host.ListenAsync(cancellationToken);
    }

    public S_LoginResponsePacket CreateLoginResponse(string accountId)
    {
        int charId = Interlocked.Increment(ref _nextCharacterId);
        string authToken = $"TOKEN_{accountId}_{charId}";

        var character = new CharacterSummary
        {
            CharacterId = charId,
            Name = string.IsNullOrWhiteSpace(accountId) ? $"모험가_{charId}" : accountId,
            Level = 1,
            MaxHp = 100
        };

        Console.WriteLine($"[LobbyServer] Login Success -> Account: {accountId}, CharId: {charId}, Name: {character.Name}");

        return new S_LoginResponsePacket
        {
            Success = true,
            AuthToken = authToken,
            WorldIp = _worldServerIp,
            WorldPort = _worldServerPort,
            Character = character
        };
    }
}
