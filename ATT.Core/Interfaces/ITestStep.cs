using ATT.Core.Attributes;

namespace ATT.Core.Interfaces;

/// <summary>
/// 测试步骤接口 — 定义测试执行的生命周期
/// 参考 OpenTAP 的 ITestStep（PrePlanRun / Run / PostPlanRun）
/// </summary>
[Display("Test Step")]
public interface ITestStep : IComponent
{
    /// <summary>步骤名称</summary>
    string Name { get; set; }

    /// <summary>是否启用</summary>
    bool Enabled { get; set; }

    /// <summary>执行结论</summary>
    Verdict Verdict { get; set; }

    /// <summary>父步骤（null 表示顶层）</summary>
    ITestStep? Parent { get; set; }

    /// <summary>子步骤列表</summary>
    IList<ITestStep> ChildSteps { get; }

    /// <summary>步骤执行前的初始化（按顺序调用）</summary>
    void PrePlanRun();

    /// <summary>步骤执行（核心逻辑）</summary>
    void Run();

    /// <summary>步骤执行后的清理（反向顺序调用）</summary>
    void PostPlanRun();

    /// <summary>
    /// 子步骤的 Verdict 发生变化时的回调
    /// </summary>
    void OnChildVerdictChanged(ITestStep child, Verdict verdict);
}
