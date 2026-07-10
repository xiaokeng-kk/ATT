namespace PacketAnalyzer.Models;

/// <summary>
/// 特征值分析结果
/// </summary>
public class AnalysisResult
{
    public int SampleCount { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double PeakToPeak { get; set; }
    public double Mean { get; set; }
    public double Variance { get; set; }
    public double StandardDeviation { get; set; }

    public override string ToString()
    {
        return $"""
                样本数 (SampleCount)    : {SampleCount}
                最小值 (Min)            : {Min:F6}
                最大值 / 峰值 (Max/Peak): {Max:F6}
                峰峰值 (PeakToPeak)     : {PeakToPeak:F6}
                均值 (Mean)             : {Mean:F6}
                方差 (Variance)         : {Variance:F6}
                标准差 (StdDev)         : {StandardDeviation:F6}
                """;
    }

    public string ToCsvLine()
    {
        return $"{SampleCount},{Min:F6},{Max:F6},{PeakToPeak:F6},{Mean:F6},{Variance:F6},{StandardDeviation:F6}";
    }
}
