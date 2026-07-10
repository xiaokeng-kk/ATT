namespace ATT.Core.Interfaces;

/// <summary>
/// 仪器接口 — 具有测量能力的设备，内部依赖 ITransport 通信
/// 对应 scopehal 的 SCPIDevice / SCPIInstrument
/// </summary>
[Attributes.Display("Instrument")]
public interface IInstrument : IComponent
{
}
