using ATT.Core.Attributes;
using ATT.Core.Base;
using ATT.Core.Interfaces;
using ATT.Core.Models;

namespace ATT.Protocol.Sensors;

/// <summary>
/// 500A 电流传感器 — 电弧故障检测系统中的电流测量前端
/// 继承 Sensor 基类，基于 TI Arc Fault Detection 通信协议（ED..9E 帧格式）
/// 通过 UART/CAN 通信接收电流波形数据和 AI 电弧检测结果
/// </summary>
[Display("500A Current Sensor", "500A range current sensor for arc fault detection",
         "Sensors", "Current")]
public class CurrentSensor500A : Sensor, IConfigurable
{
    // ==================== 协议常量 ====================
    private const byte FrameHeader = 0xED;
    private const byte FrameTrailer = 0x9E;
    private const int MinFrameLength = 4;

    // ==================== 命令字 ====================
    private const byte CmdGetFirmwareInfo    = 0x01;
    private const byte CmdGetChannelInfo     = 0x02;
    private const byte CmdSetProcessMode     = 0x03;
    private const byte CmdStartAcquisition   = 0x08;
    private const byte CmdStopAcquisition    = 0x09;
    private const byte CmdWriteConfig        = 0x0D;
    private const byte CmdReadConfig         = 0x0E;
    private const byte CmdDataUpload         = 0x11;
    private const byte CmdAiArcReport        = 0x13;

    // ==================== 配置项 ====================
    private const byte ConfigItemSamplePoints = 0x00;
    private const byte ConfigItemSampleRate   = 0x01;
    private const byte ConfigItemSampleMode   = 0x02;

    // ==================== 属性 ====================

    /// <summary>测量单位：安培</summary>
    public override string Unit => "A";

    /// <summary>额定量程 500A</summary>
    public double RatedRange => 500.0;

    /// <summary>最近一次采集到的电流波形数据（1024 个点）</summary>
    public double[]? CurrentWaveform { get; private set; }

    /// <summary>最近一次电弧检测结果</summary>
    public bool ArcDetected { get; private set; }

    /// <summary>电弧检测结果上报事件</summary>
    public event Action<bool>? ArcDetectionResult;

    /// <summary>新波形数据到达事件</summary>
    public event Action<double[]>? WaveformReceived;

    // ==================== 内部状态 ====================
    /// <summary>ADC 原始值最大范围（16-bit 有符号或 0-65535，由设备决定）</summary>
    private const double AdcMaxValue = 65535.0;
    private const int SamplesPerFrame = 1024;

    // ==================== 构造 ====================

    public CurrentSensor500A(ITransport transport) : base(transport)
    {
        Name = "500A Current Sensor";
    }

    // ==================== IConfigurable ====================

    /// <summary>
    /// Exposed configuration parameters for dynamic UI generation.
    /// </summary>
    public IReadOnlyList<ConfigurationParameter> Parameters { get; } =
    [
        // --- Actions ---
        new() { Name = "Get Firmware Info",      Description = "Query firmware version from the device",   ParameterType = ParameterType.Action },
        new() { Name = "Get Channel Info",        Description = "Query channel configuration from device", ParameterType = ParameterType.Action },
        new() { Name = "Start Acquisition",       Description = "Begin continuous current sampling",        ParameterType = ParameterType.Action },
        new() { Name = "Stop Acquisition",        Description = "Stop continuous current sampling",         ParameterType = ParameterType.Action },
        new() { Name = "Read All Config",         Description = "Read all configuration items from device",ParameterType = ParameterType.Action },

        // --- Values ---
        new() { Name = "AI Mode",                 Description = "Enable AI arc detection mode (vs data collection mode)", ParameterType = ParameterType.Boolean, DefaultValue = false },
        new() { Name = "Channel",                 Description = "Sensor channel number",                   ParameterType = ParameterType.Integer,  DefaultValue = 0, MinValue = 0, MaxValue = 255 },
        new() { Name = "Sample Points",           Description = "Number of sampling points (multiple of 1024)", ParameterType = ParameterType.Integer, DefaultValue = 1024, MinValue = 1024, MaxValue = 65535 },
        new() { Name = "Sample Rate",             Description = "Sampling rate in Hz",                     ParameterType = ParameterType.Integer, DefaultValue = 1000, MinValue = 1, MaxValue = 100000 },
        new() { Name = "Sample Mode",             Description = "Enable arc tag in waveform data",         ParameterType = ParameterType.Boolean, DefaultValue = false },
    ];

    public void SetParameter(string name, object? value)
    {
        switch (name)
        {
            case "AI Mode":          SetProcessMode(value is true, Channel); break;
            case "Channel":          _channel = Convert.ToByte(value ?? 0); SetProcessMode(_aiMode, _channel); break;
            case "Sample Points":    SetSamplePoints(Convert.ToUInt32(value ?? 1024)); break;
            case "Sample Rate":      SetSampleRate(Convert.ToUInt32(value ?? 1000)); break;
            case "Sample Mode":      SetSampleMode(value is true); break;
        }
    }

    public object? GetParameter(string name)
    {
        return name switch
        {
            "AI Mode"       => _aiMode,
            "Channel"       => Channel,
            "Sample Points" => (object)_samplePoints,
            "Sample Rate"   => (object)_sampleRate,
            "Sample Mode"   => _sampleWithArcTag,
            _               => null,
        };
    }

    public void InvokeAction(string name)
    {
        switch (name)
        {
            case "Get Firmware Info":  GetFirmwareInfo(); break;
            case "Get Channel Info":   GetChannelInfo(); break;
            case "Start Acquisition":  StartAcquisition(); break;
            case "Stop Acquisition":   StopAcquisition(); break;
            case "Read All Config":    ReadAllConfig(); break;
        }
    }

    // ==================== 内部状态 ====================

    private bool _aiMode;
    private byte _channel;
    private uint _samplePoints = 1024;
    private uint _sampleRate = 1000;
    private bool _sampleWithArcTag;

    // ==================== 公开方法 ====================

    /// <summary>
    /// 获取固件信息
    /// </summary>
    public void GetFirmwareInfo()
    {
        SendRawCmd(CmdGetFirmwareInfo);
    }

    /// <summary>
    /// 获取通道信息
    /// </summary>
    public void GetChannelInfo()
    {
        SendRawCmd(CmdGetChannelInfo);
    }

    /// <summary>
    /// 配置数据处理模式
    /// </summary>
    /// <param name="aiMode">true = AI 检测模式, false = 数据采集模式</param>
    /// <param name="channel">通道序号</param>
    public void SetProcessMode(bool aiMode, byte channel = 0)
    {
        _aiMode = aiMode;
        _channel = channel;
        byte mode = aiMode ? (byte)0x02 : (byte)0x01;
        SendCmd(CmdSetProcessMode, [mode, 0xFF, channel]);
    }

    /// <summary>
    /// 开始数据采集
    /// </summary>
    public void StartAcquisition()
    {
        SendRawCmd(CmdStartAcquisition);
    }

    /// <summary>
    /// 停止数据采集
    /// </summary>
    public void StopAcquisition()
    {
        SendRawCmd(CmdStopAcquisition);
    }

    /// <summary>
    /// 设置采样点数（必须为 1024 的整数倍）
    /// </summary>
    public void SetSamplePoints(uint points)
    {
        _samplePoints = points;
        WriteConfig(ConfigItemSamplePoints, points);
    }

    /// <summary>
    /// 设置采样速率
    /// </summary>
    public void SetSampleRate(uint rate)
    {
        _sampleRate = rate;
        WriteConfig(ConfigItemSampleRate, rate);
    }

    /// <summary>
    /// 设置采样模式
    /// </summary>
    /// <param name="withArcTag">是否带电弧标签</param>
    public void SetSampleMode(bool withArcTag)
    {
        _sampleWithArcTag = withArcTag;
        WriteConfig(ConfigItemSampleMode, withArcTag ? 1u : 0u);
    }

    /// <summary>
    /// 读取所有配置项
    /// </summary>
    public void ReadAllConfig()
    {
        SendRawCmd(CmdReadConfig);
    }

    /// <summary>Current AI mode state</summary>
    public bool AiMode => _aiMode;

    /// <summary>Current channel number</summary>
    public byte Channel => _channel;

    /// <summary>Current sample points setting</summary>
    public uint SamplePoints => _samplePoints;

    /// <summary>Current sample rate setting</summary>
    public uint SampleRate => _sampleRate;

    /// <summary>Current sample mode setting</summary>
    public bool SampleWithArcTag => _sampleWithArcTag;

    // ==================== Sensor 重写 ====================

    /// <summary>
    /// 后台数据解析循环 — 解析 ED..9E 帧格式的 arc fault 协议
    /// </summary>
    public override void DataProcessLoop()
    {
        byte[] tmpBuf = new byte[2052]; // 最大一帧：4 (overhead) + 2048 (data)

        while (!(_cts?.IsCancellationRequested ?? false))
        {
            Thread.Sleep(5);

            while (GetBufferValidLength() >= MinFrameLength)
            {
                // 找帧头 0xED
                if (_rxBuf[_pRead] != FrameHeader)
                {
                    _pRead = (_pRead + 1) % _rxBuf.Length;
                    continue;
                }

                // 尝试从帧头开始往后找帧尾 0x9E
                int avail = GetBufferValidLength();
                int trailerOffset = -1;

                for (int i = 1; i < avail; i++)
                {
                    int idx = (_pRead + i) % _rxBuf.Length;
                    if (_rxBuf[idx] == FrameTrailer)
                    {
                        trailerOffset = i;
                        break;
                    }
                }

                if (trailerOffset < MinFrameLength)
                {
                    // 没找到帧尾或帧太短，跳过帧头继续找
                    _pRead = (_pRead + 1) % _rxBuf.Length;
                    continue;
                }

                int frameLen = trailerOffset + 1;
                if (frameLen > tmpBuf.Length)
                {
                    _pRead = (_pRead + 1) % _rxBuf.Length;
                    continue;
                }

                // 拷贝完整帧到临时缓冲
                for (int i = 0; i < frameLen; i++)
                    tmpBuf[i] = _rxBuf[(_pRead + i) % _rxBuf.Length];

                _pRead = (_pRead + frameLen) % _rxBuf.Length;
                ProcessFrame(tmpBuf, frameLen);
            }
        }
    }

    // ==================== 帧处理 ====================

    /// <summary>
    /// 处理已接收的完整协议帧
    /// </summary>
    private void ProcessFrame(byte[] frame, int length)
    {
        // 帧格式: ED [cmd] [len] [data...] 9E
        // 最短帧: ED cmd 00 9E (4 bytes)
        if (length < 4 || frame[0] != FrameHeader || frame[length - 1] != FrameTrailer)
            return;

        byte cmd = frame[1];
        int dataLen = length - 3; // 去掉 ED, cmd, 9E

        switch (cmd)
        {
            case CmdDataUpload:
                ParseWaveformData(frame, length);
                break;

            case CmdAiArcReport:
                ParseArcResult(frame, length);
                break;

            case CmdWriteConfig:
                // 写配置 ACK: ED 0D 01 [item] 9E
                break;

            case CmdSetProcessMode:
                // 模式配置 ACK: ED 03 00 9E
                break;

            case CmdStartAcquisition:
                // 开始采集 ACK: ED 08 00 9E
                break;

            case CmdGetFirmwareInfo:
            case CmdGetChannelInfo:
            case CmdReadConfig:
                // 这些是查询类回复，子类或外部可重写/订阅
                break;
        }
    }

    /// <summary>
    /// 解析波形数据帧：ED 11 [len_hi] [len_lo] [2byte×1024] 9E
    /// </summary>
    private void ParseWaveformData(byte[] frame, int length)
    {
        // 帧结构: ED(1) 11(1) lenH(1) lenLo(1) data(2048) 9E(1) = 2052
        int headerSize = 4; // ED + cmd + 2 bytes length
        int dataBytes = length - headerSize - 1; // -1 for trailer

        if (dataBytes <= 0 || dataBytes % 2 != 0)
            return;

        int sampleCount = dataBytes / 2;
        double[] samples = new double[sampleCount];
        bool arcTagSampling = false;

        for (int i = 0; i < sampleCount; i++)
        {
            int offset = headerSize + i * 2;
            ushort raw = (ushort)((frame[offset] << 8) | frame[offset + 1]);

            // 检测电弧标签（最高 bit）
            bool hasArc = (raw & 0x8000) != 0;
            if (hasArc)
            {
                arcTagSampling = true;
                raw = (ushort)(raw & 0x7FFF); // 清除标签位，保留 15-bit 采样值
            }

            // 将 ADC 原始值映射到 0-500A 量程
            samples[i] = (raw / AdcMaxValue) * RatedRange;
        }

        CurrentWaveform = samples;

        // 计算平均值作为当前测量值
        double avg = 0;
        for (int i = 0; i < sampleCount; i++)
            avg += samples[i];
        avg /= sampleCount;

        OnMeasurementReceived(avg);
        WaveformReceived?.Invoke(samples);

        if (arcTagSampling)
        {
            ArcDetected = true;
            ArcDetectionResult?.Invoke(true);
        }
    }

    /// <summary>
    /// 解析 AI 电弧检测结果：ED 13 11 [result_H] [result_L] 9E
    /// </summary>
    private void ParseArcResult(byte[] frame, int length)
    {
        if (length < 6)
            return;

        ushort result = (ushort)((frame[3] << 8) | frame[4]);
        bool arc = (result == 0xFFFF);

        ArcDetected = arc;
        ArcDetectionResult?.Invoke(arc);
    }

    // ==================== 命令封装 ====================

    /// <summary>
    /// 发送无数据命令（仅帧头 + 命令 + 0x00 + 帧尾）
    /// </summary>
    private void SendRawCmd(byte cmd)
    {
        SendCommand([FrameHeader, cmd, 0x00, FrameTrailer]);
    }

    /// <summary>
    /// 发送带数据的命令
    /// </summary>
    private void SendCmd(byte cmd, byte[] payload)
    {
        int len = 3 + payload.Length; // header + cmd + payload + trailer
        byte[] buf = new byte[len + 1];
        buf[0] = FrameHeader;
        buf[1] = cmd;
        buf[2] = (byte)payload.Length;
        Array.Copy(payload, 0, buf, 3, payload.Length);
        buf[^1] = FrameTrailer;
        SendCommand(buf);
    }

    /// <summary>
    /// 写配置参数: ED 0D 05 [item] [value(4B)] 9E
    /// </summary>
    private void WriteConfig(byte item, uint value)
    {
        byte[] payload = [
            item,
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value
        ];
        SendCmd(CmdWriteConfig, payload);
    }
}
