namespace ATT.Core.Models;

/// <summary>
/// UART 串口配置
/// </summary>
public class UartConfig
{
    /// <summary>串口端口名称，如 COM1 / COM3</summary>
    public string PortName { get; init; } = "COM0";

    /// <summary>波特率 (默认 1500000)</summary>
    public int BaudRate { get; init; } = 1500000;

    /// <summary>通信超时检测 (毫秒)</summary>
    public int TimeoutMs { get; init; } = 1000;
}
