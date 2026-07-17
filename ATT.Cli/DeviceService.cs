using System.Text.Json;
using ATT.Core.Interfaces;
using ATT.Core.Models;
using ATT.Protocol.Bridges;
using ATT.Protocol.Sensors;
using ATT.Protocol.Transports;
using ATT.Cli.Models;

namespace ATT.Cli;

/// <summary>
/// 设备服务 — 读取 JSON 配置，根据 type 字段自动解析并创建设备（桥接器/传感器等）
/// </summary>
public class DeviceService : IDisposable
{
    private readonly List<IDisposable> _disposables = [];
    private bool _disposed;

    /// <summary>
    /// 从 JSON 配置文件读取并启动所有设备
    /// </summary>
    /// <param name="configPath">JSON 配置文件路径</param>
    /// <returns>已启动的设备列表</returns>
    public IReadOnlyList<object> StartFromConfig(string configPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"配置文件不存在: {configPath}");

        var json = File.ReadAllText(configPath);
        var configFile = JsonSerializer.Deserialize<DeviceConfigFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("配置文件格式错误");

        Console.WriteLine($"=== 加载设备配置: {configPath} ===");
        Console.WriteLine($"共 {configFile.Devices.Count} 个设备\n");

        var devices = new List<object>();
        foreach (var cfg in configFile.Devices)
        {
            var device = CreateDevice(cfg);
            if (device != null)
                devices.Add(device);
        }

        return devices;
    }

    /// <summary>
    /// 根据配置自动解析 type 字段，创建对应的设备实例
    /// </summary>
    private object? CreateDevice(DeviceConfig cfg)
    {
        Console.WriteLine($"--- 创建设备: {cfg.Name} ---");

        // 根据 type 解析专用配置，提取传输层描述
        BridgeExtraConfig? bridgeExtra = null;
        SensorExtraConfig? sensorExtra = null;
        TransportConfig transportCfg;

        switch (cfg.Type)
        {
            case "ZMCanBridge":
                transportCfg = ParseBridge(cfg, out bridgeExtra);
                break;
            case "CurrentSensor500A":
                transportCfg = ParseSensor(cfg, out sensorExtra);
                break;
            default:
                throw new NotSupportedException($"不支持的设备类型: {cfg.Type}");
        }

        // 1. 创建传输层
        var transport = CreateTransport(transportCfg);
        if (transport == null) return null;

        Console.WriteLine($"  传输层: {transport.Name}");

        // 2. 打开端口
        Console.Write($"  打开端口 {transportCfg.PortName}... ");
        if (!transport.Open())
        {
            Console.WriteLine("失败");
            transport.Dispose();
            return null;
        }
        Console.WriteLine("成功");

        // 3. 根据 type 创建设备并绑定到已打开的 Transport
        object? device;
        switch (cfg.Type)
        {
            case "ZMCanBridge":
                device = CreateZMBridge(cfg.Name, transport, bridgeExtra!);
                break;

            case "CurrentSensor500A":
                // 如果传感器包含桥接器，先创建桥接器（共享 Transport）
                if (sensorExtra?.Bridge != null)
                {
                    var bridge = CreateZMBridge($"{cfg.Name}-Bridge", transport, sensorExtra.Bridge);
                    _disposables.Add(bridge);
                }
                device = CreateCurrentSensor(cfg.Name, transport);
                break;

            default:
                device = null;
                break;
        }

        if (device == null)
        {
            transport.Close();
            transport.Dispose();
            return null;
        }

        _disposables.Add(transport);
        if (device is IDisposable d)
            _disposables.Add(d);

        Console.WriteLine($"  设备 {cfg.Name} 启动成功\n");
        return device;
    }

    /// <summary>
    /// 解析桥接器专用配置（transport, canBaudRate, canFrameFormat）
    /// </summary>
    private static TransportConfig ParseBridge(DeviceConfig cfg, out BridgeExtraConfig extra)
    {
        extra = JsonSerializer.Deserialize<BridgeExtraConfig>(
            JsonSerializer.Serialize(cfg.ExtensionData),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new BridgeExtraConfig();
        return extra.Transport;
    }

    /// <summary>
    /// 解析传感器专用配置（transport 或 bridge）
    /// </summary>
    private static TransportConfig ParseSensor(DeviceConfig cfg, out SensorExtraConfig extra)
    {
        extra = JsonSerializer.Deserialize<SensorExtraConfig>(
            JsonSerializer.Serialize(cfg.ExtensionData),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new SensorExtraConfig();

        // 如果包含桥接器，transport 从桥接器配置中取
        if (extra.Bridge != null)
            return extra.Bridge.Transport;

        return extra.Transport ?? throw new InvalidOperationException(
            "传感器必须配置 transport（直连）或 bridge（通过桥接器）");
    }

    /// <summary>
    /// 根据配置创建传输层实例
    /// </summary>
    private static ATT.Core.Base.Transport? CreateTransport(TransportConfig cfg)
    {
        return cfg.Type switch
        {
            "SerialPort" => new SerialPortTransport(
                cfg.PortName, cfg.BaudRate),
            _ => throw new NotSupportedException($"不支持的传输层类型: {cfg.Type}")
        };
    }

    /// <summary>
    /// 创建志明 CAN 桥接器并执行初始化配置
    /// </summary>
    private static ZMCanBridge CreateZMBridge(string name, ITransport transport, BridgeExtraConfig extra)
    {
        var bridge = new ZMCanBridge(transport)
        {
            Name = name
        };

        // 配置 CAN 波特率
        if (extra.CanBaudRate != null)
        {
            var baudConfig = new ATT.Core.Models.CanBaudRateConfig
            {
                ArbitrationBaudRate = extra.CanBaudRate.ArbitrationBaudRate,
                DataBaudRate = extra.CanBaudRate.DataBaudRate
            };
            bridge.SetCanBaudRate(baudConfig);
            Console.WriteLine($"  CAN 波特率: {baudConfig}");
        }

        // 配置 CAN 帧格式
        if (!string.IsNullOrEmpty(extra.CanFrameFormat))
        {
            var format = extra.CanFrameFormat.ToLower() switch
            {
                "standard" => CanFrameFormat.Standard,
                "extended" => CanFrameFormat.Extended,
                _ => CanFrameFormat.Standard
            };
            bridge.SetCanFrameFormat(format);
            Console.WriteLine($"  CAN 帧格式: {format}");
        }

        // 订阅 CAN 帧接收事件
        bridge.CanFrameReceived += frame =>
        {
            Console.WriteLine($"[{name}] 收到 CAN 帧: {frame}");
        };

        return bridge;
    }

    /// <summary>
    /// 创建 500A 电流传感器
    /// </summary>
    private static CurrentSensor500A CreateCurrentSensor(string name, ITransport transport)
    {
        var sensor = new CurrentSensor500A(transport)
        {
            Name = name
        };

        sensor.Subscribe(transport);
        sensor.MeasurementReceived += value =>
        {
            Console.WriteLine($"[{name}] 测量值: {value:F3} {sensor.Unit}");
        };

        return sensor;
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
