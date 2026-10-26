using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using DreamWorld.Common;
using DreamWorld.Common.Models;
using DreamWorld.Common.Protocol;

namespace DreamWorld.DummyClient;

/// <summary>
/// 서버 기능 검증 및 부하 테스트를 위한 더미 클라이언트.
/// </summary>
public static class Program
{
    private const string DefaultLobbyIp = "127.0.0.1";
    private const int DefaultLobbyPort = 10000;
    private const int DefaultWorldPort = 10001;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        using var serverCts = new CancellationTokenSource();

        // 1. 서버가 실행 중이지 않으면 백그라운드로 Lobby(:10000)와 World(:10001)를 자동 기동
        bool lobbyRunning = await IsPortOpenAsync(DefaultLobbyIp, DefaultLobbyPort);
        bool worldRunning = await IsPortOpenAsync(DefaultLobbyIp, DefaultWorldPort);

        if (!lobbyRunning)
        {
            Console.WriteLine($"[DummyClient] Lobby Server(:{DefaultLobbyPort}) 미실행 상태 감지 -> 인프로세스 백그라운드 기동");
            _ = DreamWorld.Lobby.Program.RunServerAsync(serverCts.Token);
        }
        if (!worldRunning)
        {
            Console.WriteLine($"[DummyClient] World Server(:{DefaultWorldPort}) 미실행 상태 감지 -> 인프로세스 백그라운드 기동");
            _ = DreamWorld.World.Program.RunServerAsync(serverCts.Token);
        }
        if (!lobbyRunning || !worldRunning)
        {
            await Task.Delay(250);
        }

        // 2. 실행 모드 분기 (--bots N 옵션 시 다중 봇 부하 테스트, 기본은 E2E 시나리오 검증)
        int botCount = GetArgInt(args, "--bots", 0);
        int durationSec = GetArgInt(args, "--duration", 10);

        int exitCode;
        if (botCount > 0)
        {
            exitCode = await RunMultiBotSimulationAsync(botCount, durationSec);
        }
        else
        {
            exitCode = await RunE2EVerificationSuiteAsync();
        }

        serverCts.Cancel();
        return exitCode;
    }

    /// <summary>
    /// 서버 전체 흐름을 검증하는 E2E 테스트를 실행합니다.
    /// </summary>
    private static async Task<int> RunE2EVerificationSuiteAsync()
    {
        Console.WriteLine("==================================================================");
        Console.WriteLine("  [DreamWorld E2E Verification Suite] 서버 통합 검증 시작");
        Console.WriteLine("==================================================================");
        var sw = Stopwatch.StartNew();

        try
        {
            // ------------------------------------------------------------------
            // Step 1. Lobby 서버 로그인 및 캐릭터 정보/토큰 발급 검증
            // ------------------------------------------------------------------
            Console.WriteLine("\n▶ [Step 1/6] Lobby 서버 로그인 및 캐릭터 정보 수신 검증...");
            S_LoginResponsePacket loginRes;

            using (var lobbyClient = new TcpClient())
            {
                await lobbyClient.ConnectAsync(DefaultLobbyIp, DefaultLobbyPort);
                var lobbyStream = lobbyClient.GetStream();

                byte[] req = PacketSerializer.Serialize(new C_LoginRequestPacket { AccountId = "E2E_Tester" });
                await lobbyStream.WriteAsync(req, 0, req.Length);

                var (packetId, payload) = await ReadPacketAsync(lobbyStream);
                Assert(packetId == PacketId.S_LoginResponse, $"Expected S_LoginResponse, got {packetId}");

                loginRes = PacketSerializer.Deserialize<S_LoginResponsePacket>(payload);
                Assert(loginRes.Success, "Lobby login failed.");
                Assert(!string.IsNullOrEmpty(loginRes.AuthToken), "AuthToken should not be empty.");
                Assert(loginRes.WorldPort == DefaultWorldPort, $"Expected WorldPort {DefaultWorldPort}, got {loginRes.WorldPort}");
                Console.WriteLine($"  ✔ PASS: CharId={loginRes.Character.CharacterId}, Name={loginRes.Character.Name}, Token={loginRes.AuthToken}, World={loginRes.WorldIp}:{loginRes.WorldPort}");
            }

            // ------------------------------------------------------------------
            // Step 2. World 서버 진입 및 초기 타일맵/몬스터 스냅샷 검증
            // ------------------------------------------------------------------
            Console.WriteLine("\n▶ [Step 2/6] World 서버 진입 및 몬스터 스폰 상태 검증...");
            using var worldClient = new TcpClient();
            await worldClient.ConnectAsync(loginRes.WorldIp, loginRes.WorldPort);
            var worldStream = worldClient.GetStream();

            byte[] enterReq = PacketSerializer.Serialize(new C_EnterWorldPacket
            {
                AuthToken = loginRes.AuthToken,
                CharacterId = loginRes.Character.CharacterId,
                CharacterName = loginRes.Character.Name
            });
            await worldStream.WriteAsync(enterReq, 0, enterReq.Length);

            var (enterPid, enterPayload) = await ReadPacketAsync(worldStream);
            Assert(enterPid == PacketId.S_EnterWorldSuccess, $"Expected S_EnterWorldSuccess, got {enterPid}");

            var enterRes = PacketSerializer.Deserialize<S_EnterWorldSuccessPacket>(enterPayload);
            var targetSlime = enterRes.Entities.FirstOrDefault(e =>
                e.IsMonster &&
                GridMath.WorldToTile(e.PosX, e.PosY) == (4, 4));

            Assert(targetSlime != null, "Target monster '꿈결 슬라임' at Tile(4,4) not found in snapshot!");
            Assert(targetSlime!.Hp == 50, $"Expected slime HP 50, got {targetSlime.Hp}");
            Console.WriteLine($"  ✔ PASS: MyEntityId={enterRes.MyEntityId} at ({enterRes.SpawnX:F1}, {enterRes.SpawnY:F1}), Found '{targetSlime.Name}#{targetSlime.EntityId}' at Tile(4,4) HP={targetSlime.Hp}/{targetSlime.MaxHp}");

            // ------------------------------------------------------------------
            // Step 3. 자유 이동(Free Movement) 및 벽 타일 충돌(Wall Collision) 검증
            // ------------------------------------------------------------------
            Console.WriteLine("\n▶ [Step 3/7] 자유 이동 및 타일맵 충돌 검증...");

            // 3-1) 슬라임(Tile 4,4) 바로 왼쪽 타일인 Tile(3,4) -> World(3.5, 4.5)로 이동
            byte[] moveValid = PacketSerializer.Serialize(new C_MovePacket { PosX = 3.5f, PosY = 4.5f, LookDir = Direction.Right });
            await worldStream.WriteAsync(moveValid, 0, moveValid.Length);

            var moveRes1 = await ReadExpectedPacketAsync<S_MoveBroadcastPacket>(
                worldStream,
                PacketId.S_MoveBroadcast,
                p => p.EntityId == enterRes.MyEntityId);
            Assert(Math.Abs(moveRes1.PosX - 3.5f) < 0.01f && Math.Abs(moveRes1.PosY - 4.5f) < 0.01f,
                $"Expected position (3.5, 4.5), got ({moveRes1.PosX}, {moveRes1.PosY})");
            Console.WriteLine($"  ✔ PASS (정상 이동): Tile(3,4) / World({moveRes1.PosX:F1}, {moveRes1.PosY:F1}), 바라보는 방향={moveRes1.LookDir}");

            // 3-2) 외곽 벽 타일 Tile(0,0) -> World(0.5, 0.5)로 불법 이동 시도 -> 서버가 차단하고 (3.5, 4.5) 유지해야 함
            byte[] moveWall = PacketSerializer.Serialize(new C_MovePacket { PosX = 0.5f, PosY = 0.5f, LookDir = Direction.Left });
            await worldStream.WriteAsync(moveWall, 0, moveWall.Length);

            var moveRes2 = await ReadExpectedPacketAsync<S_MoveBroadcastPacket>(
                worldStream,
                PacketId.S_MoveBroadcast,
                p => p.EntityId == enterRes.MyEntityId);
            Assert(Math.Abs(moveRes2.PosX - 3.5f) < 0.01f && Math.Abs(moveRes2.PosY - 4.5f) < 0.01f,
                $"Wall collision check failed! Server allowed moving to ({moveRes2.PosX}, {moveRes2.PosY})");
            Console.WriteLine($"  ✔ PASS (벽 타일 충돌 차단): World(0.5, 0.5) 이동 요청 거부됨 -> 위치 ({moveRes2.PosX:F1}, {moveRes2.PosY:F1}) 유지");

            // ------------------------------------------------------------------
            // Step 4. 4방향 전방 1타일 기본 공격 판정 (허공 Miss vs 전방 몬스터 Hit)
            // ------------------------------------------------------------------
            Console.WriteLine("\n▶ [Step 4/7] 내 방향 앞 1타일 기본 공격(SkillId=1) 판정 검증...");

            // 4-1) 왼쪽(Tile 2,4 - 빈 타일)을 바라보고 기본 공격 -> 0 Hit (Miss) 검증
            byte[] atkMiss = PacketSerializer.Serialize(new C_AttackPacket { SkillId = 1, LookDir = Direction.Left });
            await worldStream.WriteAsync(atkMiss, 0, atkMiss.Length);

            var atkRes1 = await ReadExpectedPacketAsync<S_AttackResultPacket>(
                worldStream,
                PacketId.S_AttackResult,
                p => p.AttackerId == enterRes.MyEntityId);
            Assert(atkRes1.Hits.Count == 0, $"Expected 0 hits when attacking empty Left tile, got {atkRes1.Hits.Count}");
            Console.WriteLine($"  ✔ PASS (빈 타일 공격): 방향={atkRes1.LookDir}(Tile 2,4) -> 적중 몬스터 없음 (Hits=0)");

            // 기본 공격 쿨타임(500ms) 대기
            await Task.Delay(550);

            // 4-2) 오른쪽(Tile 4,4 - 꿈결 슬라임 위치)을 바라보고 기본 공격 -> 1 Hit, HP 50 -> 35 검증
            byte[] atkHit = PacketSerializer.Serialize(new C_AttackPacket { SkillId = 1, LookDir = Direction.Right });
            await worldStream.WriteAsync(atkHit, 0, atkHit.Length);

            var atkRes2 = await ReadExpectedPacketAsync<S_AttackResultPacket>(
                worldStream,
                PacketId.S_AttackResult,
                p => p.AttackerId == enterRes.MyEntityId);
            Assert(atkRes2.Hits.Count == 1, $"Expected 1 hit when attacking Right tile(4,4), got {atkRes2.Hits.Count}");
            Assert(atkRes2.Hits[0].TargetEntityId == targetSlime.EntityId, "Hit wrong monster ID");
            Assert(atkRes2.Hits[0].RemainingHp == 35, $"Expected RemainingHp=35, got {atkRes2.Hits[0].RemainingHp}");
            Console.WriteLine($"  ✔ PASS (전방 1타일 적중): 방향={atkRes2.LookDir}(Tile 4,4) -> '{targetSlime.Name}' 피해={atkRes2.Hits[0].Damage}, 남은 HP={atkRes2.Hits[0].RemainingHp}");

            // ------------------------------------------------------------------
            // Step 5. 몬스터 Behavior Tree AI 추격 반응 + 원거리/타게팅 스킬 처치 검증
            // ------------------------------------------------------------------
            Console.WriteLine("\n▶ [Step 5/7] 몬스터 BT AI 추격 반응 및 원거리(3타일)/타게팅 스킬 처치 검증...");

            // 5-1) 2타일 떨어진 곳인 Tile(2,4) / World(2.5, 4.5)로 후퇴 -> 피격당한 슬라임이 BT AI에 의해 추격 이동하는지 검증
            byte[] moveBack = PacketSerializer.Serialize(new C_MovePacket { PosX = 2.5f, PosY = 4.5f, LookDir = Direction.Right });
            await worldStream.WriteAsync(moveBack, 0, moveBack.Length);
            await ReadExpectedPacketAsync<S_MoveBroadcastPacket>(
                worldStream,
                PacketId.S_MoveBroadcast,
                p => p.EntityId == enterRes.MyEntityId);

            var slimeChaseMove = await ReadExpectedPacketAsync<S_MoveBroadcastPacket>(
                worldStream,
                PacketId.S_MoveBroadcast,
                p => p.EntityId == targetSlime.EntityId,
                timeoutMs: 2000);
            Assert(slimeChaseMove.PosX < 4.5f, $"Expected slime to chase player toward left (< 4.5), got {slimeChaseMove.PosX}");
            Console.WriteLine($"  ✔ PASS (몬스터 BT 추격): 피격된 '{targetSlime.Name}'이 플레이어를 향해 이동 ({slimeChaseMove.PosX:F2}, {slimeChaseMove.PosY:F2})");

            // 5-2) 오른쪽으로 '바람의 창(전방 3타일, 데미지 25)' 시전
            byte[] rangedAtk = PacketSerializer.Serialize(new C_AttackPacket { SkillId = 2, LookDir = Direction.Right });
            await worldStream.WriteAsync(rangedAtk, 0, rangedAtk.Length);

            var atkRes3 = await ReadExpectedPacketAsync<S_AttackResultPacket>(
                worldStream,
                PacketId.S_AttackResult,
                p => p.AttackerId == enterRes.MyEntityId);
            Assert(atkRes3.Hits.Count == 1 && atkRes3.Hits[0].RemainingHp == 10, $"Expected RemainingHp=10 after Skill 2, got {atkRes3.Hits.FirstOrDefault()?.RemainingHp}");
            Console.WriteLine($"  ✔ PASS (원거리 전방 3타일 스킬): '{targetSlime.Name}' 타격 성공 -> 남은 HP={atkRes3.Hits[0].RemainingHp}");

            // 5-3) 단일 타게팅 스킬 '드림 볼트(SkillId=3, 데미지 40)'로 슬라임 마무리 처치
            byte[] targetedAtk = PacketSerializer.Serialize(new C_AttackPacket
            {
                SkillId = 3,
                LookDir = Direction.Right,
                TargetEntityId = targetSlime.EntityId
            });
            await worldStream.WriteAsync(targetedAtk, 0, targetedAtk.Length);

            var atkRes4 = await ReadExpectedPacketAsync<S_AttackResultPacket>(
                worldStream,
                PacketId.S_AttackResult,
                p => p.AttackerId == enterRes.MyEntityId);
            Assert(atkRes4.Hits.Count == 1 && atkRes4.Hits[0].IsDead && atkRes4.Hits[0].RemainingHp == 0, "Expected monster to be dead (HP=0)");
            Console.WriteLine($"  ✔ PASS (단일 타게팅 스킬 & 몬스터 처치): '{targetSlime.Name}#{targetSlime.EntityId}' 처치 완료 (HP=0, IsDead=true)");

            // ------------------------------------------------------------------
            // Step 6. World 서버 틱 루프(20 TPS)에 의한 몬스터 리스폰(5초 후) 검증
            // ------------------------------------------------------------------
            Console.WriteLine("\n▶ [Step 6/7] 몬스터 사망 5초 후 자동 리스폰 브로드캐스트 검증 (대기 중)...");
            var respawnRes = await ReadExpectedPacketAsync<S_MoveBroadcastPacket>(
                worldStream,
                PacketId.S_MoveBroadcast,
                p => p.EntityId == targetSlime.EntityId,
                timeoutMs: 6500);
            Console.WriteLine($"  ✔ PASS (몬스터 리스폰): '{targetSlime.Name}#{respawnRes.EntityId}' 스폰 위치 ({respawnRes.PosX:F1}, {respawnRes.PosY:F1})에 정상 부활!");

            // ------------------------------------------------------------------
            // Step 7. 포탈 접촉(Touch)을 통한 맵 이동 (Map 1 <-> Map 2 왕복) 검증
            // ------------------------------------------------------------------
            Console.WriteLine("\n▶ [Step 7/7] 포탈 타일 접촉 시 맵 이동 (Map 1 -> Map 2 -> Map 1) 검증...");

            // 7-1) Map 1의 포탈 위치 Tile(14, 8) / World(14.5, 8.5)로 이동 접촉 -> Map 2 진입 응답 확인
            byte[] movePortal1 = PacketSerializer.Serialize(new C_MovePacket { PosX = 14.5f, PosY = 8.5f, LookDir = Direction.Right });
            await worldStream.WriteAsync(movePortal1, 0, movePortal1.Length);

            var map2Enter = await ReadExpectedPacketAsync<S_EnterWorldSuccessPacket>(
                worldStream,
                PacketId.S_EnterWorldSuccess);
            Assert(map2Enter.MapId == 2, $"Expected MapId=2, got {map2Enter.MapId}");
            Assert(map2Enter.Entities.Any(e => e.IsMonster && e.Name.Contains("늑대")), "Expected '그림자 늑대' in Map 2 snapshot!");
            Console.WriteLine($"  ✔ PASS (Map 1 -> Map 2 워프): '{map2Enter.MapName}' 도착! 스폰=({map2Enter.SpawnX:F1}, {map2Enter.SpawnY:F1}), 몬스터 수={map2Enter.Entities.Count(e => e.IsMonster)}");

            // 포탈 재진입 쿨타임(800ms) 대기
            await Task.Delay(850);

            // 7-2) Map 2의 귀환 포탈 위치 Tile(1, 8) / World(1.5, 8.5)로 이동 접촉 -> Map 1 복귀 응답 확인
            byte[] movePortal2 = PacketSerializer.Serialize(new C_MovePacket { PosX = 1.5f, PosY = 8.5f, LookDir = Direction.Left });
            await worldStream.WriteAsync(movePortal2, 0, movePortal2.Length);

            var map1Return = await ReadExpectedPacketAsync<S_EnterWorldSuccessPacket>(
                worldStream,
                PacketId.S_EnterWorldSuccess);
            Assert(map1Return.MapId == 1, $"Expected MapId=1 on return, got {map1Return.MapId}");
            Console.WriteLine($"  ✔ PASS (Map 2 -> Map 1 귀환): '{map1Return.MapName}' 복귀! 스폰=({map1Return.SpawnX:F1}, {map1Return.SpawnY:F1})");

            sw.Stop();
            Console.WriteLine("\n==================================================================");
            Console.WriteLine($"  🎉 모든 7단계 E2E 검증 통과! (소요 시간: {sw.Elapsed.TotalSeconds:F2}초)");
            Console.WriteLine("==================================================================");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ [E2E 검증 실패] {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 다중 봇 접속 및 행동 시뮬레이션을 실행합니다.
    /// </summary>
    private static async Task<int> RunMultiBotSimulationAsync(int botCount, int durationSec)
    {
        Console.WriteLine($"[DummyClient] Starting {botCount} bots for {durationSec} seconds...");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec));
        var tasks = new List<Task>(botCount);

        for (int i = 1; i <= botCount; i++)
        {
            int botIndex = i;
            tasks.Add(Task.Run(() => RunSingleBotLoopAsync(botIndex, cts.Token)));
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"[DummyClient] {botCount} bots finished simulation cleanly.");
        return 0;
    }

    private static async Task RunSingleBotLoopAsync(int botIndex, CancellationToken ct)
    {
        try
        {
            string account = $"Bot_{botIndex:D2}";
            S_LoginResponsePacket loginRes;

            using (var lobby = new TcpClient())
            {
                await lobby.ConnectAsync(DefaultLobbyIp, DefaultLobbyPort, ct);
                var s = lobby.GetStream();
                byte[] req = PacketSerializer.Serialize(new C_LoginRequestPacket { AccountId = account });
                await s.WriteAsync(req, 0, req.Length, ct);

                var (_, payload) = await ReadPacketAsync(s);
                loginRes = PacketSerializer.Deserialize<S_LoginResponsePacket>(payload);
            }

            using var world = new TcpClient();
            await world.ConnectAsync(loginRes.WorldIp, loginRes.WorldPort, ct);
            var ws = world.GetStream();

            byte[] enter = PacketSerializer.Serialize(new C_EnterWorldPacket
            {
                AuthToken = loginRes.AuthToken,
                CharacterId = loginRes.Character.CharacterId,
                CharacterName = loginRes.Character.Name
            });
            await ws.WriteAsync(enter, 0, enter.Length, ct);

            // 수신 버퍼 비우기 루프
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        await ReadPacketAsync(ws);
                    }
                }
                catch { }
            }, ct);

            var rng = new Random(botIndex * 1000 + Environment.TickCount);
            float x = 3.5f, y = 3.5f;
            Direction[] dirs = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

            while (!ct.IsCancellationRequested)
            {
                Direction dir = dirs[rng.Next(dirs.Length)];
                var (dx, dy) = GridMath.GetDirectionOffset(dir);
                float nx = Math.Clamp(x + dx * 0.5f, 1.5f, 14.5f);
                float ny = Math.Clamp(y + dy * 0.5f, 1.5f, 14.5f);
                x = nx;
                y = ny;

                byte[] move = PacketSerializer.Serialize(new C_MovePacket { PosX = x, PosY = y, LookDir = dir });
                await ws.WriteAsync(move, 0, move.Length, ct);

                byte[] atk = PacketSerializer.Serialize(new C_AttackPacket { SkillId = 1, LookDir = dir });
                await ws.WriteAsync(atk, 0, atk.Length, ct);

                await Task.Delay(550, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task<TPacket> ReadExpectedPacketAsync<TPacket>(
        NetworkStream stream,
        PacketId expectedId,
        Func<TPacket, bool>? predicate = null,
        int timeoutMs = 3000)
        where TPacket : IPacket, new()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            int remaining = Math.Max(100, timeoutMs - (int)sw.ElapsedMilliseconds);
            var (packetId, payload) = await ReadPacketAsync(stream, remaining);
            if (packetId != expectedId)
            {
                continue;
            }

            var pkt = PacketSerializer.Deserialize<TPacket>(payload);
            if (predicate == null || predicate(pkt))
            {
                return pkt;
            }
        }

        throw new TimeoutException($"Timed out waiting for packet {expectedId}.");
    }

    private static async Task<(PacketId packetId, byte[] payload)> ReadPacketAsync(NetworkStream stream, int timeoutMs = 3000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        byte[] lenBuf = new byte[2];
        await ReadExactAsync(stream, lenBuf, 2, cts.Token);

        ushort bodyLen = BitConverter.ToUInt16(lenBuf, 0);
        byte[] body = new byte[bodyLen];
        await ReadExactAsync(stream, body, bodyLen, cts.Token);

        ushort rawId = BitConverter.ToUInt16(body, 0);
        byte[] payload = new byte[bodyLen - 2];
        Buffer.BlockCopy(body, 2, payload, 0, payload.Length);
        return ((PacketId)rawId, payload);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (n <= 0)
            {
                throw new EndOfStreamException("Disconnected from server while reading packet.");
            }
            offset += n;
        }
    }

    private static async Task<bool> IsPortOpenAsync(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(150);
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetArgInt(string[] args, string key, int defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(args[i + 1], out int val))
            {
                return val;
            }
        }
        return defaultValue;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
