namespace ATT.Core.Interfaces;

/// <summary>
/// 传输层接口 — 负责通信端口的开关、连接状态及原始数据收发
/// 对应 scopehal 的 SCPITransport/SerialTransport
/// </summary>
public interface ITransport : IComponent
{
    /// <summary>传输层名称</summary>
    string Name { get; }

    /// <summary>当前是否已连接</summary>
    bool IsConnected { get; }

    /// <summary>打开端口</summary>
    bool Open();

    /// <summary>关闭端口</summary>
    bool Close();

    /// <summary>发送原始数据</summary>
    void SendRaw(byte[] data);

    /// <summary>接收到原始数据事件</summary>
    event Action<byte[]>? DataReceived;

    /// <summary>连接状态变更事件</summary>
    event Action<bool>? ConnectionStateChanged;
}
