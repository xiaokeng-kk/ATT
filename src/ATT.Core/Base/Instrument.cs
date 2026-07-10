using ATT.Core.Interfaces;

namespace ATT.Core.Base;

/// <summary>
/// 仪器基类 — 扩展 Component，构造函数注入 ITransport
/// 对应 scopehal 的 SCPIDevice：Instrument(ITransport transport) 组合通信层
/// </summary>
public abstract class Instrument : Component, IInstrument
{
    /// <summary>传输层实例（构造函数注入）</summary>
    protected ITransport Transport { get; }

    protected Instrument(ITransport transport)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        Name = "Instrument";
    }

    /// <summary>
    /// 获取仪器标识（类似 SCPI *IDN?）
    /// </summary>
    public virtual string Identity { get; protected set; } = string.Empty;
}
