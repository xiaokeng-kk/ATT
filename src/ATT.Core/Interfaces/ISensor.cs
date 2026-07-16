using ATT.Core.Attributes;

namespace ATT.Core.Interfaces;

/// <summary>
/// 传感器接口 — 可测量物理量的设备
/// 扩展 IInstrument，添加配置命令、读取测量值、后台数据持续接收三个功能维度
/// 对应实际仪器中的各类传感器（温度/电压/压力/电流等）
/// </summary>
[Display("Sensor", "A sensor that measures physical quantities")]
public interface ISensor : IInstrument
{
    // ==================== 基本属性 ====================

    /// <summary>传感器名称</summary>
    string Name { get; }

    // ==================== 配置命令 ====================

    /// <summary>
    /// 发送配置命令到传感器（如设置量程、模式、采样率等）
    /// </summary>
    void SendCommand(byte[] command);

    // ==================== 读取接口 ====================

    /// <summary>
    /// 读取当前测量值
    /// </summary>
    double ReadValue();

    /// <summary>
    /// 测量值的工程单位（如 V, A, °C, Pa, Hz 等）
    /// </summary>
    string Unit { get; }

    /// <summary>新测量数据到达事件（后台解析完成后触发）</summary>
    event Action<double>? MeasurementReceived;

    // ==================== 后台数据解析 ====================

    /// <summary>
    /// 订阅 Transport — 桥接消费其 DataReceived 事件
    /// 绑定后自动接收原始数据并放入解析缓冲区
    /// </summary>
    void Subscribe(ITransport transport);

    /// <summary>
    /// 后台数据解析循环 — 从缓冲区读取原始数据并解析为测量值
    /// </summary>
    void DataProcessLoop();

    /// <summary>
    /// 启动后台数据解析线程
    /// </summary>
    void StartDataProcessThread();

    /// <summary>
    /// 停止后台数据解析线程
    /// </summary>
    void StopDataProcessThread();
}
