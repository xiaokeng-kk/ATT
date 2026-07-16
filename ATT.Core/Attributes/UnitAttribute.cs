namespace ATT.Core.Attributes;

/// <summary>
/// 单位特性 — 为属性标注物理单位，支持量纲缩放
/// 参考 OpenTAP 的 [Unit(unit, PreScaling)] 模式
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class UnitAttribute : Attribute
{
    /// <summary>单位字符串，如 "s"、"Hz"、"V"</summary>
    public string Unit { get; }

    /// <summary>预缩放系数，如 PreScaling=1000 表示存储值为毫秒，显示为秒</summary>
    public double PreScaling { get; }

    public UnitAttribute(string unit, double preScaling = 1.0)
    {
        Unit = unit;
        PreScaling = preScaling;
    }
}
