using System.Net.Sockets;

namespace DreamWorld.Server.Core.Network;

/// <summary>
/// 외부 서버로의 아웃바운드 TCP 연결을 관리하는 클라이언트.
/// </summary>
public static class TcpConnector
{
    /// <summary>
    /// 대상 서버에 비동기로 연결하고 세션을 시작합니다.
    /// </summary>
    public static async Task<TSession> ConnectAsync<TSession>(
        string host,
        int port,
        Func<TSession> sessionFactory,
        CancellationToken cancellationToken = default)
        where TSession : TcpSession
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken);
            TSession session = sessionFactory();
            session.Attach(client);
            _ = session.RunReceiveLoopAsync(cancellationToken);
            return session;
        }
        catch
        {
            client.Close();
            throw;
        }
    }

    /// <summary>
    /// 대상 서버와의 연결을 유지하며 연결 종료 시 자동으로 재연결합니다.
    /// </summary>
    public static async Task MaintainConnectionAsync<TSession>(
        string peerName,
        string host,
        int port,
        Func<TSession> sessionFactory,
        int retryDelayMs = 3000,
        CancellationToken cancellationToken = default)
        where TSession : TcpSession
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(host, port, cancellationToken);
                TSession session = sessionFactory();
                session.Attach(client);
                Console.WriteLine($"[TcpConnector] Connected to peer '{peerName}' ({host}:{port}).");

                await session.RunReceiveLoopAsync(cancellationToken);
                Console.WriteLine($"[TcpConnector] Disconnected from peer '{peerName}' ({host}:{port}).");
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                client.Close();
                Console.WriteLine($"[TcpConnector] Failed to connect to '{peerName}' ({host}:{port}): {ex.Message}. Retrying in {retryDelayMs}ms...");
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(retryDelayMs, cancellationToken);
            }
        }
    }
}
