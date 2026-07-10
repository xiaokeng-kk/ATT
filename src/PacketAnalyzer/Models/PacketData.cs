namespace PacketAnalyzer.Models;

/// <summary>
/// 存储解析后的报文数据
/// </summary>
public class PacketData
{
    public List<double> Values { get; set; } = [];
    public string Source { get; set; } = string.Empty;
    public int SampleCount => Values.Count;
}
