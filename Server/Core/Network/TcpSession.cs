using System.Net;
using System.Net.Sockets;
using System.Text;
using DreamWorld.Common;
using DreamWorld.Common.Protocol;

namespace DreamWorld.Server.Core.Network;

/// <summary>
/// TCP 연결의 패킷 송수신을 처리하는 세션 추상 클래스.
/// </summary>
public abstract class TcpSession : IDisposable
{
    private readonly object _sendLock = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _isDisconnected;

    public EndPoint? RemoteEndPoint { get; private set; }
    public bool IsConnected => _client != null && _client.Connected && _isDisconnected == 0;

    /// <summary>
    /// 연결된 TcpClient를 세션에 바인딩합니다.
    /// </summary>
    public void Attach(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        RemoteEndPoint = client.Client.RemoteEndPoint;
        _isDisconnected = 0;
    }

    /// <summary>
    /// 패킷 수신 루프를 실행합니다.
    /// </summary>
    public async Task RunReceiveLoopAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null || _stream == null)
        {
            throw new InvalidOperationException("TcpSession is not attached to a TcpClient.");
        }

        using var reg = cancellationToken.Register(Disconnect);

        try
        {
            OnConnected();

            byte[] lenBuf = new byte[2];
            while (!cancellationToken.IsCancellationRequested && _isDisconnected == 0)
            {
                int headerRead = await ReadExactAsync(_stream, lenBuf, 2, cancellationToken);
                if (headerRead < 2)
                {
                    break;
                }

                ushort bodyLength = BitConverter.ToUInt16(lenBuf, 0);
                if (bodyLength < 2)
                {
                    break;
                }

                byte[] bodyBuf = new byte[bodyLength];
                int bodyRead = await ReadExactAsync(_stream, bodyBuf, bodyLength, cancellationToken);
                if (bodyRead < bodyLength)
                {
                    break;
                }

                using var ms = new MemoryStream(bodyBuf);
                using var reader = new BinaryReader(ms, Encoding.UTF8);

                var packetId = (PacketId)reader.ReadUInt16();
                OnPacketReceived(packetId, reader);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) when (_isDisconnected == 1 || cancellationToken.IsCancellationRequested) { }
        catch (SocketException) when (_isDisconnected == 1 || cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            OnError(ex);
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 패킷 객체를 직렬화하여 전송합니다.
    /// </summary>
    public void Send(IPacket packet)
    {
        SendRaw(PacketSerializer.Serialize(packet));
    }

    /// <summary>
    /// 직렬화된 패킷 바이트 배열을 전송합니다.
    /// </summary>
    public void SendRaw(byte[] packetBytes)
    {
        if (_isDisconnected != 0 || _stream == null)
        {
            return;
        }

        try
        {
            lock (_sendLock)
            {
                _stream.Write(packetBytes, 0, packetBytes.Length);
            }
        }
        catch
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 세션 연결을 종료합니다.
    /// </summary>
    public void Disconnect()
    {
        if (Interlocked.Exchange(ref _isDisconnected, 1) != 0)
        {
            return;
        }

        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }

        OnDisconnected();
    }

    public void Dispose()
    {
        Disconnect();
    }

    protected virtual void OnConnected() { }
    protected abstract void OnPacketReceived(PacketId packetId, BinaryReader reader);
    protected virtual void OnDisconnected() { }
    protected virtual void OnError(Exception ex) { }

    private static async Task<int> ReadExactAsync(
        NetworkStream stream,
        byte[] buffer,
        int count,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken);
            if (n <= 0)
            {
                return offset;
            }
            offset += n;
        }
        return offset;
    }
}
