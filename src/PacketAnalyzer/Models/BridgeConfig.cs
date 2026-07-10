using System.Text.Json.Serialization;

namespace PacketAnalyzer.Models;

/// <summary>
/// 顶层配置模型 — 对应 bridge-config.json 的根对象
/// </summary>
public class BridgeConfigFile
{
    [JsonPropertyName("bridges")]
    public List<BridgeConfig> Bridges { get; set; } = [];
}

/// <summary>
/// 单个桥接器配置
/// </summary>
public class BridgeConfig
{
    /// <summary>桥接器实例名称</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>桥接器类型，如 "ZMCanBridge"</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>传输层配置</summary>
    [JsonPropertyName("transport")]
    public TransportConfig Transport { get; set; } = new();

    /// <summary>
    /// CAN 波特率配置（可选，仅 ZMCanBridge 需要）
    /// </summary>
    [JsonPropertyName("canBaudRate")]
    public CanBaudRateConfig? CanBaudRate { get; set; }

    /// <summary>
    /// CAN 帧格式（可选，仅 ZMCanBridge 需要）
    /// "Standard" 或 "Extended"
    /// </summary>
    [JsonPropertyName("canFrameFormat")]
    public string? CanFrameFormat { get; set; }
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
