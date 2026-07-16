using ATT.Core.Interfaces;

namespace ATT.Core.Base;

/// <summary>
/// 传输层基类 — 实现 ITransport，提供连接状态管理和数据收发骨架
/// 对应 scopehal 的 SCPITransport / SerialTransport
/// </summary>
public abstract class Transport : Component, ITransport, IDisposable
{
    private bool _isConnected;
    private bool _isConnectedTemp;

    /// <summary>当前是否已连接</summary>
    public bool IsConnected
    {
        get => _isConnected;
        protected set
        {
            _isConnected = value;
            if (_isConnected != _isConnectedTemp)
            {
                _isConnectedTemp = _isConnected;
                ConnectionStateChanged?.Invoke(_isConnected);
            }
        }
    }

    /// <summary>打开连接（子类实现）</summary>
    public abstract bool Open();

    /// <summary>关闭连接（子类实现）</summary>
    public abstract bool Close();

    /// <summary>发送原始数据（子类实现）</summary>
    public abstract void SendRaw(byte[] data);

    /// <summary>接收到原始数据事件</summary>
    public event Action<byte[]>? DataReceived;

    /// <summary>连接状态变更事件</summary>
    public event Action<bool>? ConnectionStateChanged;

    /// <summary>
    /// 触发 DataReceived 事件 — 子类接收到数据后调用
    /// </summary>
    protected void OnDataReceived(byte[] data)
    {
        DataReceived?.Invoke(data);
    }

    // ==================== IDisposable ====================

    /// <summary>释放资源，默认调用 Close()</summary>
    public virtual void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }
}
