using System.Reflection;
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
/// 支持 IConfigurable 参数注入、TCP 传输层，提供运行时快照输出
/// </summary>
public class DeviceService : IDisposable
{
    private readonly List<IDisposable> _disposables = [];
    private readonly List<object> _devices = [];
    private bool _disposed;

    /// <summary>
    /// 从 JSON 配置文件读取并启动所有设备
    /// </summary>
    /// <param name="configPath">JSON 配置文件路径</param>
    /// <param name="parameters">可选的全局参数字典（覆盖设备配置中的 parameters）</param>
    /// <returns>已启动的设备列表</returns>
    public IReadOnlyList<object> StartFromConfig(string configPath, Dictionary<string, object>? parameters = null)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"配置文件不存在: {configPath}");

        var json = File.ReadAllText(configPath);
        var configFile = JsonSerializer.Deserialize<DeviceConfigFile>(json) ?? throw new InvalidOperationException("配置文件格式错误");

        Console.WriteLine($"=== 加载设备配置: {configPath} ===");
        Console.WriteLine($"共 {configFile.Devices.Count} 个设备\n");

        foreach (var cfg in configFile.Devices)
        {
            var device = CreateDevice(cfg);
            if (device != null)
                _devices.Add(device);
        }

        return _devices.AsReadOnly();
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

        // 4. 应用 IConfigurable 参数
        ApplyParameters(device, cfg);

        _disposables.Add(transport);
        if (device is IDisposable d)
            _disposables.Add(d);

        Console.WriteLine($"  设备 {cfg.Name} 启动成功\n");
        return device;
    }

    /// <summary>
    /// 获取传输层配置 — 优先使用 Transport 显式属性，其次从 ExtensionData 解析
    /// </summary>
    private static TransportConfig GetTransportConfig(DeviceConfig cfg)
    {
        // 优先使用显式 Transport 属性
        if (cfg.Transport != null)
            return cfg.Transport;

        // 从 ExtensionData 中提取
        if (cfg.ExtensionData.TryGetValue("transport", out var transportElement) &&
            transportElement.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<TransportConfig>(
                transportElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new TransportConfig();
        }

        return new TransportConfig();
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
        return GetTransportConfig(cfg);
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

        return GetTransportConfig(cfg);
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
            "Tcp" => CreateTcpTransport(cfg),
            _ => throw new NotSupportedException($"不支持的传输层类型: {cfg.Type}")
        };
    }

    /// <summary>
    /// 创建 TCP 传输层实例
    /// </summary>
    private static ATT.Core.Base.Transport CreateTcpTransport(TransportConfig cfg)
    {
        var host = cfg.Host ?? "127.0.0.1";
        int port = cfg.RemotePort ?? 8080;

        // 通过反射构造 TcpTransport — 因为 TcpTransport 可能在不同的命名空间/项目中
        // 实际类型: ATT.Protocol.Transports.TcpTransport (待实现)
        var transport = new ATT.Protocol.Transports.TcpTransport(host, port)
        {
            Name = $"TCP-{host}:{port}"
        };
        return transport;
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

    // ==================== IConfigurable 参数注入 ====================

    /// <summary>
    /// 如果设备实现了 IConfigurable，从配置中提取 parameters 并逐项设置
    /// 支持 DeviceConfig.Parameters 直接属性 和 ExtensionData["parameters"] 两种来源
    /// </summary>
    private static void ApplyParameters(object device, DeviceConfig cfg)
    {
        if (device is not IConfigurable configurable)
            return;

        Dictionary<string, object?>? parameters = null;

        // 优先使用 DeviceConfig.Parameters 直接属性
        if (cfg.Parameters != null)
        {
            parameters = cfg.Parameters;
        }
        // 回退到从 ExtensionData 提取
        else if (cfg.ExtensionData.TryGetValue("parameters", out var paramsElement) &&
                 paramsElement.ValueKind == JsonValueKind.Object)
        {
            parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                paramsElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        if (parameters == null) return;

        foreach (var (name, value) in parameters)
        {
            if (value == null) continue;
            try
            {
                configurable.SetParameter(name, value);
                Console.WriteLine($"  参数 [{name}] = {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  参数 [{name}] 设置失败: {ex.Message}");
            }
        }
    }

    // ==================== 运行时快照 ====================

    /// <summary>
    /// 收集所有已启动设备的运行时状态，用于输出到 stdout 供 UI 捕获
    /// </summary>
    public RuntimeOutput GetRuntimeOutput()
    {
        var runtimeDevices = new List<RuntimeDevice>();

        foreach (var device in _devices)
        {
            var rd = new RuntimeDevice
            {
                Name = GetDeviceName(device),
                Type = device.GetType().Name,
                Connected = true, // 能走到这里说明连接成功
            };

            // ISensor — 读取 Unit 和当前值
            if (device is ISensor sensor)
            {
                rd.Unit = sensor.Unit;
                rd.ReadValue = sensor.ReadValue();
            }
            else if (device is IInstrument)
            {
                // IInstrument 可能有自定义属性
                rd.Unit = TryGetProperty<string>(device, "Unit") ?? "";
                rd.ReadValue = TryGetProperty<double>(device, "ReadValue");
            }

            // IDisplayable — 获取 UI 控件 JSON
            if (device is IDisplayable displayable)
            {
                rd.DisplayJson = displayable.GetDisplayJson();
            }

            // IConfigurable — 获取参数快照
            if (device is IConfigurable configurable)
            {
                rd.Parameters = configurable.Parameters.Select(p => new RuntimeParameter
                {
                    Name = p.Name,
                    Description = p.Description,
                    ParameterType = p.ParameterType.ToString(),
                    CurrentValue = p.CurrentValue ?? p.DefaultValue,
                    MinValue = p.MinValue,
                    MaxValue = p.MaxValue,
                    EnumOptions = p.EnumOptions?.Select(e => $"{e.Name}={e.Value}").ToArray(),
                }).ToList();
            }

            runtimeDevices.Add(rd);
        }

        return new RuntimeOutput
        {
            Status = "connected",
            Devices = runtimeDevices
        };
    }

    /// <summary>获取设备名称（通过 Name 属性或 type name）</summary>
    private static string GetDeviceName(object device)
    {
        var prop = device.GetType().GetProperty("Name");
        return prop?.GetValue(device)?.ToString() ?? device.GetType().Name;
    }

    /// <summary>尝试通过反射读取属性值</summary>
    private static T? TryGetProperty<T>(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (prop == null) return default;
        var val = prop.GetValue(obj);
        if (val is T t) return t;
        return default;
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
