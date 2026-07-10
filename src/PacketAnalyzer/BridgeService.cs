using System.Text.Json;
using ATT.Protocol;
using ATT.Protocol.Bridges;
using ATT.Protocol.Models;
using ATT.Protocol.Transports;
using PacketAnalyzer.Models;

namespace PacketAnalyzer;

/// <summary>
/// 桥接器服务 — 读取 JSON 配置，创建并启动桥接器
/// </summary>
public class BridgeService : IDisposable
{
    private readonly List<IDisposable> _disposables = [];
    private bool _disposed;

    /// <summary>
    /// 从 JSON 配置文件读取并启动所有桥接器
    /// </summary>
    /// <param name="configPath">JSON 配置文件路径</param>
    /// <returns>已启动的 ICanBridge 列表</returns>
    public IReadOnlyList<ICanBridge> StartFromConfig(string configPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"配置文件不存在: {configPath}");

        var json = File.ReadAllText(configPath);
        var configFile = JsonSerializer.Deserialize<BridgeConfigFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("配置文件格式错误");

        Console.WriteLine($"=== 加载桥接器配置: {configPath} ===");
        Console.WriteLine($"共 {configFile.Bridges.Count} 个桥接器\n");

        var bridges = new List<ICanBridge>();
        foreach (var cfg in configFile.Bridges)
        {
            var bridge = CreateBridge(cfg);
            if (bridge != null)
                bridges.Add(bridge);
        }

        return bridges;
    }

    /// <summary>
    /// 根据配置创建一个桥接器
    /// </summary>
    private ICanBridge? CreateBridge(BridgeConfig cfg)
    {
        Console.WriteLine($"--- 创建桥接器: {cfg.Name} ---");

        // 1. 创建传输层
        var transport = CreateTransport(cfg.Transport);
        if (transport == null) return null;

        Console.WriteLine($"  传输层: {transport.Name}");

        // 2. 打开端口
        Console.Write($"  打开端口 {cfg.Transport.PortName}... ");
        if (!transport.Open())
        {
            Console.WriteLine("失败");
            transport.Dispose();
            return null;
        }
        Console.WriteLine("成功");

        // 3. 创建桥接器
        ICanBridge? bridge = cfg.Type switch
        {
            "ZMCanBridge" => CreateZMBridge(cfg, transport),
            _ => throw new NotSupportedException($"不支持的桥接器类型: {cfg.Type}")
        };

        if (bridge == null)
        {
            transport.Close();
            transport.Dispose();
            return null;
        }

        // 4. 订阅 CAN 帧接收事件
        bridge.CanFrameReceived += frame =>
        {
            Console.WriteLine($"[{cfg.Name}] 收到 CAN 帧: {frame}");
        };

        _disposables.Add(transport);
        if (bridge is IDisposable d)
            _disposables.Add(d);

        Console.WriteLine($"  桥接器 {cfg.Name} 启动成功\n");
        return bridge;
    }

    /// <summary>
    /// 根据配置创建传输层实例
    /// </summary>
    private static ATT.Core.Base.Transport? CreateTransport(TransportConfig cfg)
    {
        return cfg.Type switch
        {
            "SerialPort" => new SerialPortTransport(
                cfg.PortName, cfg.BaudRate/*, cfg.TimeoutMs*/),
            _ => throw new NotSupportedException($"不支持的传输层类型: {cfg.Type}")
        };
    }

    /// <summary>
    /// 创建志明 CAN 桥接器并执行初始化配置
    /// </summary>
    private static ZMCanBridge? CreateZMBridge(BridgeConfig cfg, ATT.Core.Interfaces.ITransport transport)
    {
        var bridge = new ZMCanBridge(transport)
        {
            Name = cfg.Name
        };

        // 配置 CAN 波特率
        if (cfg.CanBaudRate != null)
        {
            var baudConfig = new ATT.Protocol.Models.CanBaudRateConfig
            {
                ArbitrationBaudRate = cfg.CanBaudRate.ArbitrationBaudRate,
                DataBaudRate = cfg.CanBaudRate.DataBaudRate
            };
            bridge.SetCanBaudRate(baudConfig);
            Console.WriteLine($"  CAN 波特率: {baudConfig}");
        }

        // 配置 CAN 帧格式
        if (!string.IsNullOrEmpty(cfg.CanFrameFormat))
        {
            var format = cfg.CanFrameFormat.ToLower() switch
            {
                "standard" => CanFrameFormat.Standard,
                "extended" => CanFrameFormat.Extended,
                _ => CanFrameFormat.Standard
            };
            bridge.SetCanFrameFormat(format);
            Console.WriteLine($"  CAN 帧格式: {format}");
        }

        return bridge;
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
            // 逆序释放
            for (int i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i].Dispose();
            _disposables.Clear();
        }
        _disposed = true;
    }
}
