using ATT.Core.Interfaces;
using ATT.Protocol.Models;

namespace ATT.Protocol.Bridges;

/// <summary>
/// 志明 CAN 桥接器 — 针对志明 CAN-UART 转换器硬件的具体实现
/// 继承 CanBridge 虚类，复用协议帧打包/解析基础设施
/// </summary>
public class ZMCanBridge : CanBridge
{
    // ==================== 命令字 ====================
    private const byte CmdComCheck = 0x00;
    private const byte CmdSetUartBaud = 0x01;
    private const byte CmdReadUartBaud = 0x02;
    private const byte CmdSetCanBaud = 0x03;
    private const byte CmdReadCanBaud = 0x04;
    private const byte CmdSetCanFrameFormat = 0x0B;

    /// <summary>
    /// 创建志明 CAN 桥接器
    /// </summary>
    public ZMCanBridge(ITransport transport) : base(transport)
    {
        Name = "ZhiMing CAN Bridge";
    }

    /// <summary>
    /// 检测通信是否正常（发送 ping 等待应答）
    /// </summary>
    public bool CheckCommunication()
    {
        ReadCmd(CmdComCheck);
        return true; // 子类可扩展为等待应答逻辑
    }

    /// <summary>设置 UART 波特率</summary>
    public void SetUartBaudRate(int baudRate)
    {
        WriteCmd(CmdSetUartBaud,
            [(byte)baudRate, (byte)(baudRate >> 8), (byte)(baudRate >> 16)]);
    }

    /// <summary>读取 UART 波特率</summary>
    public void ReadUartBaudRate()
    {
        ReadCmd(CmdReadUartBaud);
    }

    /// <summary>设置 CAN 波特率（仲裁段 + 数据段）</summary>
    public void SetCanBaudRate(CanBaudRateConfig config)
    {
        byte[] buf = new byte[10];
        buf[0] = 0x00;

        switch (config.ArbitrationBaudRate)
        {
            case 125:
                buf[1] = 0x00; buf[2] = 0x0C;
                buf[3] = 0x01; buf[4] = 0x27; buf[5] = 0x00;
                break;
            case 250:
                buf[1] = 0x02; buf[2] = 0x0B;
                buf[3] = 0x02; buf[4] = 0x13; buf[5] = 0x00;
                break;
            case 500:
                buf[1] = 0x02; buf[2] = 0x0B;
                buf[3] = 0x02; buf[4] = 0x09; buf[5] = 0x00;
                break;
            case 1000:
                buf[1] = 0x01; buf[2] = 0x04;
                buf[3] = 0x01; buf[4] = 0x09; buf[5] = 0x00;
                break;
        }

        switch (config.DataBaudRate)
        {
            case 2:
                buf[6] = 0x00; buf[7] = 0x06;
                buf[8] = 0x01; buf[9] = 0x03;
                break;
            case 4:
                buf[6] = 0x00; buf[7] = 0x06;
                buf[8] = 0x01; buf[9] = 0x01;
                break;
            case 5:
                buf[6] = 0x03; buf[7] = 0x0A;
                buf[8] = 0x03; buf[9] = 0x00;
                break;
            case 1000:
                buf[6] = 1; buf[7] = 14;
                buf[8] = 3; buf[9] = 3;
                break;
        }

        WriteCmd(CmdSetCanBaud, buf);
    }

    /// <summary>读取 CAN 波特率</summary>
    public void ReadCanBaudRate()
    {
        ReadCmd(CmdReadCanBaud);
    }

    /// <summary>设置 CAN 帧格式</summary>
    public void SetCanFrameFormat(CanFrameFormat format)
    {
        WriteCmd(CmdSetCanFrameFormat, [(byte)format]);
    }

    // ==================== 底层命令封装 ====================

    private void ReadCmd(byte cmd)
    {
        const byte frameLen = 7;
        byte[] txBuf = [0x55, 0xAA, frameLen, 0x01, cmd, 0x00, 0x5A];
        txBuf[5] = CalculateXor(txBuf);
        SendRaw(txBuf);
    }

    private void WriteCmd(byte cmd, byte[] payload)
    {
        int frameLen = 7 + payload.Length;
        byte[] txBuf = new byte[frameLen];
        txBuf[0] = 0x55;
        txBuf[1] = 0xAA;
        txBuf[2] = (byte)frameLen;
        txBuf[3] = 0x01;
        txBuf[4] = cmd;
        Array.Copy(payload, 0, txBuf, 5, payload.Length);
        txBuf[frameLen - 2] = CalculateXor(txBuf);
        txBuf[frameLen - 1] = 0x5A;
        SendRaw(txBuf);
    }
}
