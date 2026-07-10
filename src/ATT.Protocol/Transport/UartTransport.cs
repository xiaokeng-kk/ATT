using ATT.Protocol.Models;

namespace ATT.Protocol.Transports;

/// <summary>
/// UART 传输层虚类 — 在 Transport 基础上增加 UART 配置管理
/// 子类实现具体的串口打开/关闭/读写（如 SerialPortTransport）
/// </summary>
public abstract class UartTransport : ATT.Core.Base.Transport
{
    /// <summary>UART 串口配置（端口名、波特率、超时等）</summary>
    public UartConfig Config { get; private set; }

    /// <summary>
    /// 使用指定配置创建 UART 传输层
    /// </summary>
    /// <param name="config">串口配置</param>
    protected UartTransport(UartConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 更新配置（如果已连接则先关闭再重开）
    /// </summary>
    public void Configure(UartConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        bool wasConnected = IsConnected;
        if (wasConnected) Close();

        Config = config;

        if (wasConnected) Open();
    }
}
