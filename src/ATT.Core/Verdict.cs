namespace ATT.Core;

/// <summary>
/// 执行结论（参考 OpenTAP Verdict）
/// </summary>
public enum Verdict
{
    /// <summary>未执行</summary>
    None = 0,

    /// <summary>通过</summary>
    Pass = 1,

    /// <summary>失败</summary>
    Fail = 2,

    /// <summary>出错（异常）</summary>
    Error = 3,

    /// <summary>无法判定</summary>
    Inconclusive = 4,
}
