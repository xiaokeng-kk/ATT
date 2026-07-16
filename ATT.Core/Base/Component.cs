using System.Diagnostics;
using ATT.Core.Interfaces;

namespace ATT.Core.Base;

/// <summary>
/// 组件基类 — 提供 Name 和 Log
/// 参考 OpenTAP 的 Resource 基类（简化版）
/// </summary>
public abstract class Component : IComponent
{
    /// <summary>组件名称（赋值时自动创建 TraceSource 日志）</summary>
    public virtual string Name
    {
        get => _name;
        set
        {
            _name = value;
            Log = new TraceSource(value);
        }
    }
    private string _name = "N/A";

    /// <summary>日志源，使用 Log.Info() / Log.Debug() / Log.Error()</summary>
    public TraceSource? Log { get; protected set; }
}
