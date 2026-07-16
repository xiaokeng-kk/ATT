namespace ATT.Core.Models;

/// <summary>
/// CAN 波特率配置
/// </summary>
public class CanBaudRateConfig
{
    /// <summary>仲裁段波特率 (kbps)：125 / 250 / 500 / 1000</summary>
    public int ArbitrationBaudRate { get; init; }

    /// <summary>数据段波特率 (kbps)：2 / 4 / 5 / 1000</summary>
    public int DataBaudRate { get; init; } = 2;

    public override string ToString()
    {
        return $"Arbitration={ArbitrationBaudRate}kbps, Data={DataBaudRate}kbps";
    }
}
