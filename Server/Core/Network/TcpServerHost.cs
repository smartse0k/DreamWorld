using System.Net;
using System.Net.Sockets;

namespace DreamWorld.Server.Core.Network;

/// <summary>
/// TCP 연결을 수락하고 세션을 생성하는 서버 호스트.
/// </summary>
public class TcpServerHost
{
    private readonly string _serverName;
    private readonly int _port;
    private readonly Func<TcpSession> _sessionFactory;

    public int Port => _port;

    public TcpServerHost(string serverName, int port, Func<TcpSession> sessionFactory)
    {
        _serverName = serverName;
        _port = port;
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public async Task ListenAsync(CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        using var reg = cancellationToken.Register(() => listener.Stop());

        Console.WriteLine($"[{_serverName}] Listening on port {_port}...");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
                TcpSession session = _sessionFactory();
                session.Attach(client);
                _ = session.RunReceiveLoopAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            listener.Stop();
        }
    }
}
