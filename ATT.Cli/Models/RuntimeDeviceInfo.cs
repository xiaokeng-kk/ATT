using System.Text.Json.Serialization;

namespace ATT.Cli.Models;

/// <summary>
/// 运行时设备输出 — CLI 启动所有设备后通过 stdout 输出
/// UI 进程捕获后据此切换到运行时视图
/// </summary>
public class RuntimeOutput
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "connected";

    [JsonPropertyName("devices")]
    public List<RuntimeDevice> Devices { get; set; } = [];
}

/// <summary>
/// 单个设备的运行时快照
/// </summary>
public class RuntimeDevice
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "";

    [JsonPropertyName("readValue")]
    public double ReadValue { get; set; }

    /// <summary>
    /// IDisplayable.GetDisplayJson() 返回的 JSON 字符串
    /// 如果设备未实现 IDisplayable，此字段为空
    /// </summary>
    [JsonPropertyName("displayJson")]
    public string? DisplayJson { get; set; }
}
