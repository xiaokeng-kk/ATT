using System.Net.Sockets;
using ATT.Core.Base;

namespace ATT.Protocol.Transports;

/// <summary>
/// TCP 传输层 — 基于 System.Net.Sockets.TcpClient 的 ITransport 实现
/// 用于通过网络远程连接设备（嵌入式设备 / 远程传感器等）
/// </summary>
public class TcpTransport : Transport
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();

    /// <summary>TCP 地址（IP 或主机名）</summary>
    public string Host { get; }

    /// <summary>TCP 端口</summary>
    public int Port { get; }

    /// <summary>连接超时（毫秒）</summary>
    public int ConnectTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// 创建 TCP 传输层
    /// </summary>
    /// <param name="host">IP 地址或主机名</param>
    /// <param name="port">端口号</param>
    public TcpTransport(string host = "127.0.0.1", int port = 8080)
    {
        Host = host;
        Port = port;
        Name = $"TCP-{host}:{port}";
    }

    /// <summary>
    /// 打开 TCP 连接
    /// </summary>
    public override bool Open()
    {
        lock (_lock)
        {
            if (IsConnected) return true;

            try
            {
                _client = new TcpClient();
                var connectTask = _client.ConnectAsync(Host, Port)
                    .WaitAsync(TimeSpan.FromMilliseconds(ConnectTimeoutMs));
                connectTask.GetAwaiter().GetResult();

                _stream = _client.GetStream();
                _cts = new CancellationTokenSource();

                // 启动后台接收线程
                _ = Task.Run(ReceiveLoop, _cts.Token);

                IsConnected = true;
                Console.WriteLine($"[{Name}] TCP 连接成功");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{Name}] TCP 连接失败: {ex.Message}");
                IsConnected = false;
                return false;
            }
        }
    }

    /// <summary>
    /// 关闭 TCP 连接
    /// </summary>
    public override bool Close()
    {
        lock (_lock)
        {
            if (!IsConnected) return true;

            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                _stream?.Close();
                _stream?.Dispose();
                _stream = null;

                _client?.Close();
                _client?.Dispose();
                _client = null;

                IsConnected = false;
                Console.WriteLine($"[{Name}] TCP 连接已关闭");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{Name}] 关闭 TCP 连接失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 发送原始数据
    /// </summary>
    public override void SendRaw(byte[] data)
    {
        if (_stream == null || !IsConnected)
            throw new InvalidOperationException($"TCP {Host}:{Port} 未连接");

        lock (_lock)
        {
            _stream.Write(data, 0, data.Length);
        }
    }

    /// <summary>
    /// 后台接收循环 — 在独立线程中持续读取 TCP 数据流
    /// </summary>
    private async Task ReceiveLoop()
    {
        byte[] buffer = new byte[4096];

        try
        {
            while (!(_cts?.IsCancellationRequested ?? true))
            {
                if (_stream == null) break;

                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _cts?.Token ?? CancellationToken.None);
                if (bytesRead <= 0) break; // 连接已关闭

                byte[] chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                OnDataReceived(chunk);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch (IOException)
        {
            // 连接断开
        }
        catch (ObjectDisposedException)
        {
            // 资源已释放
        }
        finally
        {
            IsConnected = false;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public override void Dispose()
    {
        Close();
        base.Dispose();
    }
}
