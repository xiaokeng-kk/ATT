using PacketAnalyzer.Models;

namespace PacketAnalyzer.Analyzers;

/// <summary>
/// 特征值计算器 — 计算峰值、均值、方差、标准差等
/// </summary>
public static class FeatureAnalyzer
{
    /// <summary>
    /// 对报文数据执行特征值分析
    /// </summary>
    public static AnalysisResult Analyze(PacketData data)
    {
        var values = data.Values;
        if (values.Count == 0)
            throw new InvalidOperationException("没有数据可供分析。");

        int n = values.Count;
        double min = values[0];
        double max = values[0];
        double sum = 0;

        foreach (var v in values)
        {
            if (v < min) min = v;
            if (v > max) max = v;
            sum += v;
        }

        double mean = sum / n;

        // 方差: Σ(xi - mean)² / n  (总体方差)
        double sumSquaredDiff = 0;
        foreach (var v in values)
        {
            double diff = v - mean;
            sumSquaredDiff += diff * diff;
        }

        double variance = sumSquaredDiff / n;
        double stdDev = Math.Sqrt(variance);

        return new AnalysisResult
        {
            SampleCount = n,
            Min = min,
            Max = max,
            PeakToPeak = max - min,
            Mean = mean,
            Variance = variance,
            StandardDeviation = stdDev
        };
    }
}
