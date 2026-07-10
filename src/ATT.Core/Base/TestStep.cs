using ATT.Core.Interfaces;

namespace ATT.Core.Base;

/// <summary>
/// 测试步骤基类 — 实现 ITestStep 生命周期
/// 参考 OpenTAP 的 TestStep（PrePlanRun → Run → PostPlanRun）
/// </summary>
public abstract class TestStep : ITestStep
{
    private ITestStep? _parent;
    private Verdict _verdict;

    protected TestStep()
    {
        Name = GetType().Name;
        ChildSteps = [];
    }

    /// <summary>步骤名称</summary>
    public string Name { get; set; }

    /// <summary>是否启用（禁用的步骤跳过执行）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>执行结论</summary>
    public Verdict Verdict
    {
        get => _verdict;
        set
        {
            _verdict = value;
            // 传递给父步骤
            Parent?.OnChildVerdictChanged(this, value);
        }
    }

    /// <summary>父步骤（由容器步骤在添加子步骤时自动设置）</summary>
    public ITestStep? Parent
    {
        get => _parent;
        set
        {
            if (_parent != value)
            {
                _parent = value;
                OnParentChanged();
            }
        }
    }

    /// <summary>子步骤列表（容器步骤使用）</summary>
    public IList<ITestStep> ChildSteps { get; }

    // ==================== 生命周期方法（可重写） ====================

    /// <summary>步骤执行前的初始化</summary>
    public virtual void PrePlanRun()
    {
    }

    /// <summary>步骤执行（核心逻辑 — 子类必须实现）</summary>
    public abstract void Run();

    /// <summary>步骤执行后的清理</summary>
    public virtual void PostPlanRun()
    {
    }

    // ==================== 虚拟回调 ====================

    /// <summary>
    /// 父步骤发生变化时调用
    /// </summary>
    protected virtual void OnParentChanged()
    {
    }

    /// <summary>
    /// 子步骤的 Verdict 发生变化时调用（仅容器步骤需要处理）
    /// </summary>
    public virtual void OnChildVerdictChanged(ITestStep child, Verdict verdict)
    {
    }

    public override string ToString()
    {
        return $"{Name} [{Verdict}]";
    }
}
