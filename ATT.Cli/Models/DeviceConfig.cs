using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATT.Cli.Models;

/// <summary>
/// 顶层配置模型 — 对应 config.json 的根对象
/// 支持 "devices" 或 "sensor" 两种根 key
/// </summary>
public class DeviceConfigFile
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];

    /// <summary>设备列表（自动从 "devices" 或 "sensor" 中读取）</summary>
    [JsonIgnore]
    public List<DeviceConfig> Devices
    {
        get
        {
            foreach (var key in new[] { "devices", "sensor", "sensors" })
            {
                if (ExtensionData.TryGetValue(key, out var element) &&
                    element.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<DeviceConfig>>(
                        element.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? [];
                }
            }
            return [];
        }
    }
}

/// <summary>
/// 单个设备配置
/// 顶层只定义 name / type，其他属性由 type 派发到对应的专用配置
/// </summary>
public class DeviceConfig
{
    /// <summary>设备实例名称</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// 设备类型，如 "ZMCanBridge" / "CurrentSensor500A"
    /// CLI 据此自动解析并创建对应的设备实例
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    /// 传输层配置（显式属性，优先于 ExtensionData["transport"]）
    /// </summary>
    [JsonPropertyName("transport")]
    public TransportConfig? Transport { get; set; }

    /// <summary>
    /// IConfigurable 参数 — 设备创建后逐项设置
    /// 例：{ "AI Mode": true, "Channel": 1, "Sample Rate": 1000 }
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; set; }

    /// <summary>其他属性（按 type 路由到不同的配置类）</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
}

// ===== type 对应的专用配置 =====

/// <summary>桥接器设备额外配置</summary>
public class BridgeExtraConfig
{
    [JsonPropertyName("transport")]
    public TransportConfig Transport { get; set; } = new();

    [JsonPropertyName("canBaudRate")]
    public CanBaudRateConfig? CanBaudRate { get; set; }

    [JsonPropertyName("canFrameFormat")]
    public string? CanFrameFormat { get; set; }
}

/// <summary>传感器设备额外配置</summary>
public class SensorExtraConfig
{
    /// <summary>直连 Transport（无桥接器时使用）</summary>
    [JsonPropertyName("transport")]
    public TransportConfig? Transport { get; set; }

    /// <summary>通过桥接器连接（可选，优先于 transport）</summary>
    [JsonPropertyName("bridge")]
    public BridgeExtraConfig? Bridge { get; set; }
}

/// <summary>
/// 传输层配置
/// </summary>
public class TransportConfig
{
    /// <summary>传输层类型，如 "SerialPort" / "Tcp"</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>串口端口名，如 COM3</summary>
    [JsonPropertyName("portName")]
    public string PortName { get; set; } = "COM1";

    /// <summary>波特率</summary>
    [JsonPropertyName("baudRate")]
    public int BaudRate { get; set; } = 1500000;

    /// <summary>超时时间（毫秒）</summary>
    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 1000;

    /// <summary>TCP 地址（host:port 格式，如 "192.168.1.100:8080"）</summary>
    [JsonPropertyName("host")]
    public string? Host { get; set; }

    /// <summary>TCP 端口</summary>
    [JsonPropertyName("remotePort")]
    public int? RemotePort { get; set; }
}

/// <summary>
/// CAN 波特率配置
/// </summary>
public class CanBaudRateConfig
{
    /// <summary>仲裁段波特率 (kbps)</summary>
    [JsonPropertyName("arbitrationBaudRate")]
    public int ArbitrationBaudRate { get; set; } = 500;

    /// <summary>数据段波特率 (kbps)</summary>
    [JsonPropertyName("dataBaudRate")]
    public int DataBaudRate { get; set; } = 2;
}
