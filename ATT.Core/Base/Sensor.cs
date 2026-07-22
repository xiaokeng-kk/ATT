using System.Reflection;
using System.Text.Json;
using ATT.Core.Interfaces;
using ATT.Core.Models;

namespace ATT.Core.Base;

/// <summary>
/// 传感器基类 — 扩展 Instrument，实现 ISensor
/// 提供：环形接收缓冲区、后台数据解析线程、传输层事件订阅
/// 子类需重写 DataProcessLoop 实现具体的协议解析和测量值提取
/// </summary>
public abstract class Sensor : Instrument, ISensor, IDisplayable, IDisposable
{
    // ==================== 常量 ====================
    private const int RxBufferSize = 4096;

    // ==================== 环形接收缓冲区 ====================
    /// <summary>环形缓冲区 — OnTransportDataReceived 写入，DataProcessLoop 读取</summary>
    protected readonly byte[] _rxBuf = new byte[RxBufferSize];
    /// <summary>缓冲区写入指针</summary>
    protected int _pWrite;
    /// <summary>缓冲区读取指针</summary>
    protected int _pRead;

    // ==================== 后台线程 ====================
    /// <summary>后台解析线程</summary>
    protected Thread? _dataProcessThread;
    /// <summary>取消令牌</summary>
    protected CancellationTokenSource? _cts;

    // ==================== 内部状态 ====================
    private ITransport? _subscribedTransport;
    private bool _disposed;

    /// <summary>最近一次解析到的测量值（供 ReadValue 返回）</summary>
    protected double _lastValue;

    // ==================== 构造 ====================

    protected Sensor(ITransport transport) : base(transport)
    {
        Name = "Sensor";
    }

    // ==================== ISensor — 基本属性 ====================

    /// <summary>测量值的工程单位（子类必须提供）</summary>
    public abstract string Unit { get; }

    // ==================== ISensor — 配置命令 ====================

    /// <summary>
    /// 发送配置命令到传感器（默认通过 Transport.SendRaw 发送）
    /// 子类可重写以添加协议打包逻辑
    /// </summary>
    public virtual void SendCommand(byte[] command)
    {
        Transport.SendRaw(command);
    }

    // ==================== ISensor — 读取接口 ====================

    /// <summary>
    /// 读取当前测量值（默认返回 _lastValue，由后台解析更新）
    /// 子类可重写为同步查询方式
    /// </summary>
    public virtual double ReadValue() => _lastValue;

    /// <summary>新测量数据到达事件（后台解析完成后触发）</summary>
    public event Action<double>? MeasurementReceived;

    /// <summary>
    /// 触发 MeasurementReceived 事件 — 子类解析到新值后调用
    /// </summary>
    protected void OnMeasurementReceived(double value)
    {
        _lastValue = value;
        MeasurementReceived?.Invoke(value);
    }

    // ==================== ISensor — 后台数据解析 ====================

    /// <summary>
    /// 订阅 Transport — 绑定其 DataReceived 事件
    /// 首次订阅时自动启动后台解析线程
    /// </summary>
    public void Subscribe(ITransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (_subscribedTransport != transport)
        {
            // 取消旧订阅
            if (_subscribedTransport != null)
                _subscribedTransport.DataReceived -= OnTransportDataReceived;

            _subscribedTransport = transport;
            _subscribedTransport.DataReceived += OnTransportDataReceived;
        }

        if (_dataProcessThread == null)
            StartDataProcessThread();
    }

    /// <summary>
    /// 后台数据解析循环 — 默认实现为空循环（仅监听取消信号）
    /// 子类必须重写以实现具体的协议帧解析和测量值提取
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

    /// <summary>
    /// 启动后台数据解析线程（调用虚方法 DataProcessLoop）
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
    /// 停止后台数据解析线程
    /// </summary>
    public virtual void StopDataProcessThread()
    {
        _cts?.Cancel();
        _cts = null;
        _dataProcessThread = null;
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

    // ==================== 辅助方法 ====================

    /// <summary>获取环形缓冲区有效数据长度 — 供子类 DataProcessLoop 使用</summary>
    protected int GetBufferValidLength()
    {
        return (_pWrite + RxBufferSize - _pRead) % RxBufferSize;
    }

    // ==================== IDisplayable ====================

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// 返回 JSON 格式的 UI 控件描述。
    /// 默认从嵌入式资源 "{FullTypeName}.ui.json" 读取。
    /// 子类可重写以运行时动态生成 JSON。
    /// </summary>
    public virtual string GetDisplayJson()
    {
        var type = GetType();
        var resourceName = $"{type.FullName}.ui.json";
        var assembly = type.GetTypeInfo().Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return """{"elements":[]}""";

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 返回解析后的 UI 元素列表。
    /// 默认实现反序列化 GetDisplayJson() 中的 "elements" 数组。
    /// 子类可重写以编程方式构建列表。
    /// </summary>
    public virtual IReadOnlyList<UiElement> GetDisplayElements()
    {
        var json = GetDisplayJson();
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("elements", out var elementsProp))
            {
                var elements = JsonSerializer.Deserialize<List<UiElement>>(
                    elementsProp.GetRawText(), _jsonOptions);
                return elements ?? [];
            }
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
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
            if (_subscribedTransport != null)
                _subscribedTransport.DataReceived -= OnTransportDataReceived;
        }
        _disposed = true;
    }
}
