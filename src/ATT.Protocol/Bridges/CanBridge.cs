using ATT.Core.Interfaces;
using ATT.Protocol.Models;

namespace ATT.Protocol.Bridges;

/// <summary>
/// CAN 桥接器虚类 — PC 连接 CAN 节点的通用基础设施
/// 提供：协议帧打包/解析、环形缓冲区、后台解析线程
/// 子类可重写 ProcessParsedFrame 添加设备特定的命令处理
/// </summary>
public abstract class CanBridge : ICanBridge, IDisposable
{
    // ==================== 协议常量 ====================
    protected const int RxBufferSize = 60000;
    protected const int MinFrameLength = 7;
    protected const byte FrameHeader1 = 0x55;
    protected const byte FrameHeader2 = 0xAA;
    protected const byte FrameTrailer = 0x5A;

    // ==================== 命令字 ====================
    private const byte CmdSendCanData = 0x10;
    private const byte CmdReceiveCanData = 0x11;

    // ==================== 字段 ====================
    private ITransport? _transport;
    private bool _disposed;

    /// <summary>环形接收缓冲区 — 子类 DataProcessLoop 从中读取数据</summary>
    protected readonly byte[] _rxBuf = new byte[RxBufferSize];
    /// <summary>环形缓冲区写入指针 — OnTransportDataReceived 写入</summary>
    protected int _pWrite;
    /// <summary>环形缓冲区读取指针 — DataProcessLoop 读取</summary>
    protected int _pRead;

    /// <summary>后台处理线程 — StartDataProcessThread 创建</summary>
    protected Thread? _dataProcessThread;
    /// <summary>后台线程取消令牌</summary>
    protected CancellationTokenSource? _cts;

    // ==================== 构造 ====================

    /// <summary>
    /// 创建 CAN 桥接器（之后需调用 Subscribe 绑定 Transport）
    /// </summary>
    protected CanBridge()
    {
    }

    /// <summary>
    /// 创建 CAN 桥接器并自动订阅 Transport
    /// </summary>
    protected CanBridge(ITransport transport)
    {
        Subscribe(transport);
    }

    // ==================== ICanBridge ====================

    public string Name { get; set; } = "CAN Bridge";

    /// <summary>
    /// 订阅 Transport — 桥接消费其 DataReceived 事件
    /// 支持重新绑定（自动取消旧订阅）
    /// </summary>
    public void Subscribe(ITransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (_transport != transport)
        {
            // 取消旧订阅
            if (_transport != null)
                _transport.DataReceived -= OnTransportDataReceived;

            _transport = transport;
            _transport.DataReceived += OnTransportDataReceived;
        }

        if (_dataProcessThread == null)
            StartDataProcessThread();
    }

    /// <summary>
    /// 发送 CAN 数据帧 — 按协议打包后通过 _transport.SendRaw() 发送
    /// </summary>
    public void SendCanFrame(CanFrame frame)
    {
        if (_transport == null)
            throw new InvalidOperationException("必须调用 Subscribe(ITransport) 后才能发送数据");

        bool isExtended = frame.FrameFormat == CanFrameFormat.Extended;
        int idBytes = isExtended ? 4 : 2;
        int frameLen = 7 + idBytes + 1 + frame.Data.Length;

        byte[] txBuf = new byte[frameLen];
        int idx = 0;

        txBuf[idx++] = FrameHeader1;
        txBuf[idx++] = FrameHeader2;
        txBuf[idx++] = (byte)frameLen;
        txBuf[idx++] = 0x01;
        txBuf[idx++] = CmdSendCanData;
        txBuf[idx++] = 0x00;
        txBuf[idx++] = 0x00;
        txBuf[idx++] = 0x00;
        txBuf[idx++] = (byte)frame.FrameFormat;
        txBuf[idx++] = 0x00;

        for (int i = 0; i < idBytes; i++)
            txBuf[idx++] = (byte)(frame.Id >> (i * 8));

        txBuf[idx++] = (byte)frame.Data.Length;
        Array.Copy(frame.Data, 0, txBuf, idx, frame.Data.Length);
        idx += frame.Data.Length;

        byte checkValue = 0;
        for (int j = 0; j < frameLen - 2; j++)
            checkValue ^= txBuf[j];
        txBuf[idx++] = checkValue;
        txBuf[idx] = FrameTrailer;

        _transport.SendRaw(txBuf);
    }

    /// <summary>接收到已解析的 CAN 数据帧</summary>
    public event Action<CanFrame>? CanFrameReceived;

    // ==================== 子类扩展点 ====================

    /// <summary>
    /// 发送原始协议数据 — 供子类发送自定义命令
    /// </summary>
    protected void SendRaw(byte[] data)
    {
        if (_transport == null)
            throw new InvalidOperationException("必须调用 Subscribe(ITransport) 后才能发送数据");
        _transport.SendRaw(data);
    }

    /// <summary>
    /// 处理已解析的协议帧 — 子类可重写以处理设备特定的命令
    /// </summary>
    protected virtual void ProcessParsedFrame(byte[] frame, int length)
    {
        if (frame[4] == CmdReceiveCanData)
            HandleReceivedCanFrame(frame);
    }

    // ==================== 传输层数据接收 ====================

    private void OnTransportDataReceived(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            _rxBuf[_pWrite] = data[i];
            _pWrite = (_pWrite + 1) % RxBufferSize;
        }
    }

    // ==================== 后台帧解析 ====================

    /// <summary>
    /// 后台数据帧解析循环 — 默认实现为空循环（仅监听取消信号）
    /// 子类可重写以添加自定义帧解析逻辑
    /// </summary>
    public virtual void DataProcessLoop()
    {
        try
        {
            while (!(_cts?.IsCancellationRequested ?? false))
            {
                Thread.Sleep(50);
            }
        }
        catch (ThreadInterruptedException)
        {
            // 线程被中断，正常退出
        }
    }

    protected virtual void HandleReceivedCanFrame(byte[] frame)
    {
        byte frameFormat = frame[8];
        bool isExtended = frameFormat != 0x00;
        int dataStart = isExtended ? 15 : 13;

        int id = isExtended
            ? frame[10] | (frame[11] << 8) | (frame[12] << 16) | (frame[13] << 24)
            : frame[10] | (frame[11] << 8);

        int dataLen = frame[dataStart - 1];
        byte[] data = new byte[dataLen];
        Array.Copy(frame, dataStart, data, 0, dataLen);

        var canFrame = new CanFrame
        {
            Id = id,
            FrameFormat = (CanFrameFormat)frameFormat,
            Data = data
        };

        CanFrameReceived?.Invoke(canFrame);
    }

    // ==================== 辅助方法 ====================

    /// <summary>获取环形缓冲区有效数据长度 — 供子类 DataProcessLoop 使用</summary>
    protected int GetBufferValidLength()
    {
        return (_pWrite + RxBufferSize - _pRead) % RxBufferSize;
    }

    /// <summary>
    /// 计算 XOR 校验值 — 供子类发送自定义命令时使用
    /// </summary>
    protected static byte CalculateXor(byte[] buf, int? length = null)
    {
        int len = length ?? buf.Length;
        byte check = 0;
        for (int i = 0; i < len; i++)
            check ^= buf[i];
        return check;
    }

    /// <summary>
    /// 启动后台数据帧解析线程
    /// </summary>
    public virtual void StartDataProcessThread()
    {
        _cts = new CancellationTokenSource();
        _dataProcessThread = new Thread(DataProcessLoop)
        {
            IsBackground = true,
            Name = $"{Name}-DataProcess"
        };
        _dataProcessThread.Start();
    }

    /// <summary>
    /// 停止后台数据帧解析线程
    /// </summary>
    public virtual void StopDataProcessThread()
    {
        _cts?.Cancel();
        _cts = null;
        _dataProcessThread = null;
    }

    // ==================== IDisposable ====================

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            StopDataProcessThread();
            if (_transport != null)
                _transport.DataReceived -= OnTransportDataReceived;
        }
        _disposed = true;
    }
}
