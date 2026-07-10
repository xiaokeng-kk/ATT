using ATT.Core.Interfaces;
using ATT.Protocol.Models;

namespace ATT.Protocol;

/// <summary>
/// CAN 桥接器接口 — PC 连接 CAN 节点的通用接口
/// 只负责：发送时协议打包 → Transport 发送，接收时解析 → 事件通知
/// 不管理连接生命周期，连接由 Transport 层自行管理
/// </summary>
public interface ICanBridge
{
    /// <summary>桥接器名称</summary>
    string Name { get; }

    /// <summary>
    /// 订阅 Transport — 桥接消费其 DataReceived / ConnectionStateChanged 事件
    /// 绑定后桥接自动接收原始数据并解析 CAN 帧
    /// </summary>
    void Subscribe(ITransport transport);

    /// <summary>
    /// 发送 CAN 数据帧（自动按协议打包后通过 Transport 发送）
    /// </summary>
    void SendCanFrame(CanFrame frame);

    /// <summary>接收到已解析的 CAN 数据帧</summary>
    event Action<CanFrame>? CanFrameReceived;
}
