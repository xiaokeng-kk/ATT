# Device Communication Architecture

> 基于实际代码的两层架构文档。协议解析不提取为独立层，而是内嵌在设备（Sensor / CanBridge）内部。

---

## 目录

1. [分层架构总览](#1-分层架构总览)
2. [传输层 — Transport](#2-传输层--transport)
3. [应用层 — Application](#3-应用层--application)
4. [传感器（Sensor）](#4-传感器sensor)
5. [CAN 桥接器（CanBridge）](#5-can-桥接器canbridge)
6. [数据流](#6-数据流)
7. [UI / 配置层](#7-ui--配置层)
8. [配置与启动](#8-配置与启动)
9. [已实现 vs 未实现](#9-已实现-vs-未实现)

---

## 1. 分层架构总览

```
┌──────────────────────────────────────────────────────────────┐
│                    Application Layer                           │
│   ATT.Protocol / ATT.Core.Base                                 │
│                                                                │
│   Sensor (继承 Instrument)                                      │
│     ├── 构造函数注入 ITransport                                 │
│     ├── Subscribe(ITransport) — 订阅 DataReceived              │
│     ├── 环缓冲 _rxBuf + 后台 DataProcessLoop                    │
│     ├── 子类实现 ED..9E / 其他协议帧解析                         │
│     └── 实现 IDisplayable / IConfigurable（可选）               │
│                                                                │
│   CanBridge（独立基础设施）                                      │
│     ├── Subscribe(ITransport) — 订阅 DataReceived              │
│     ├── 环缓冲 _rxBuf + 后台 DataProcessLoop                    │
│     ├── 0x55/0xAA 帧同步 + XOR 校验                             │
│     └── CanFrameReceived 事件 — 输出已解析的 CAN 帧              │
│                                                                │
├──────────────────────────────────────────────────────────────┤
│                    Transport Layer                              │
│   ITransport — 字节流传输                                       │
│     ├── SerialPortTransport  串口通信（System.IO.Ports）        │
│     └── TcpTransport         TCP/IP 通信                        │
│   - SendRaw(byte[])                                             │
│   - DataReceived(event)                                         │
│   - 只关心字节收发，不关心帧结构                                 │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. 传输层 — Transport

### 2.1 ITransport 接口

```csharp
// ATT.Core/Interfaces/ITransport.cs
public interface ITransport : IComponent
{
    string Name { get; }
    bool IsConnected { get; }
    bool Open();
    bool Close();
    void SendRaw(byte[] data);
    event Action<byte[]>? DataReceived;
    event Action<bool>? ConnectionStateChanged;
}
```

**职责：**
- 管理物理端口的开/关和连接状态
- 字节级收发，不关心帧结构
- `DataReceived` 事件 — 裸字节数组
- `ConnectionStateChanged` 事件 — 连接/断开通知

### 2.2 Transport 基类

```csharp
// ATT.Core/Base/Transport.cs
public abstract class Transport : Component, ITransport, IDisposable
{
    public bool IsConnected { get; protected set; }
    public abstract bool Open();
    public abstract bool Close();
    public abstract void SendRaw(byte[] data);
    public event Action<byte[]>? DataReceived;
    public event Action<bool>? ConnectionStateChanged;

    /// <summary>子类接收到数据后调用</summary>
    protected void OnDataReceived(byte[] data);
    public virtual void Dispose();
}
```

### 2.3 UartTransport

```csharp
// ATT.Core/Base/UartTransport.cs
public abstract class UartTransport : Transport
{
    public UartConfig Config { get; private set; }
    protected UartTransport(UartConfig config);
    public void Configure(UartConfig config);
}
```

### 2.4 具体实现类

#### SerialPortTransport

```csharp
// ATT.Protocol/Transports/SerialPortTransport.cs
public class SerialPortTransport : UartTransport
{
    public SerialPortTransport(string portName, int baudRate = 1500000);
    public override bool Open();    // 创建 SerialPort, 绑定 DataReceived/ErrorReceived
    public override bool Close();   // 解绑事件、关闭、释放
    public override void SendRaw(byte[] data);

    // OnPortDataReceived → port.Read → OnDataReceived(buffer)
    // OnPortErrorReceived → TXFull/Overrun → 自动重连
}
```

#### TcpTransport

```csharp
// ATT.Protocol/Transports/TcpTransport.cs
public class TcpTransport : Transport
{
    public TcpTransport(string host, int port, int connectTimeoutMs = 5000);
    public override bool Open();    // 创建 TcpClient, 启动 ReceiveLoop
    public override bool Close();
    public override void SendRaw(byte[] data);

    // ReceiveLoop → 后台 Task → networkStream.Read → OnDataReceived
}
```

---

## 3. 应用层 — Application

### 3.1 IComponent（标记接口）

```csharp
// ATT.Core/Interfaces/IComponent.cs
public interface IComponent { }
```

### 3.2 Component 基类

```csharp
// ATT.Core/Base/Component.cs
public abstract class Component : IComponent
{
    public virtual string Name { get; set; }  // setter 自动创建 TraceSource Log
    public TraceSource? Log { get; protected set; }
}
```

### 3.3 IInstrument

```csharp
// ATT.Core/Interfaces/IInstrument.cs
[Display("Instrument")]
public interface IInstrument : IComponent { }
```

### 3.4 Instrument 基类

```csharp
// ATT.Core/Base/Instrument.cs
public abstract class Instrument : Component, IInstrument
{
    protected ITransport Transport { get; }  // 构造函数注入
    protected Instrument(ITransport transport);
    public virtual string Identity { get; protected set; }
}
```

**设计原则：** Instrument 不直接操作硬件端口，通过 `ITransport` 收发数据。不同的通信方式（串口/TCP）注入不同的 `ITransport` 实现。

---

## 4. 传感器（Sensor）

### 4.1 ISensor 接口

```csharp
// ATT.Core/Interfaces/ISensor.cs
[Display("Sensor", "A sensor that measures physical quantities")]
public interface ISensor : IInstrument
{
    string Name { get; }
    void SendCommand(byte[] command);
    double ReadValue();
    string Unit { get; }
    event Action<double>? MeasurementReceived;

    // 数据源
    void Subscribe(ITransport transport);

    // 后台解析
    void DataProcessLoop();
    void StartDataProcessThread();
    void StopDataProcessThread();
}
```

### 4.2 Sensor 基类（环形缓冲区 + 后台线程 + IDisplayable）

```csharp
// ATT.Core/Base/Sensor.cs
public abstract class Sensor : Instrument, ISensor, IDisplayable, IDisposable
{
    // === 环形缓冲区（4096 字节）===
    protected readonly byte[] _rxBuf = new byte[4096];
    protected int _pWrite;       // 写入指针 — OnTransportDataReceived 写入
    protected int _pRead;        // 读取指针 — DataProcessLoop 读取

    // === 后台线程 ===
    protected Thread? _dataProcessThread;
    protected CancellationTokenSource? _cts;
    protected double _lastValue;  // 最近一次解析到的测量值

    // === 构造 ===
    protected Sensor(ITransport transport) : base(transport) { Name = "Sensor"; }

    // === ISensor ===
    public abstract string Unit { get; }
    public virtual void SendCommand(byte[] command) => Transport.SendRaw(command);
    public virtual double ReadValue() => _lastValue;
    public event Action<double>? MeasurementReceived;
    protected void OnMeasurementReceived(double value);

    // === Subscribe ===
    public void Subscribe(ITransport transport)
    {
        // 取消旧订阅 → 绑定新 transport.DataReceived → 启动后台线程
        _subscribedTransport.DataReceived += OnTransportDataReceived;
        if (_dataProcessThread == null) StartDataProcessThread();
    }

    // === DataProcessLoop（子类必须重写）===
    // 默认实现: while(!cancel) Thread.Sleep(50)
    public virtual void DataProcessLoop();

    // === 环缓冲辅助 ===
    protected int GetBufferValidLength();  // (pWrite + RxBufferSize - pRead) % RxBufferSize

    // === IDisplayable（嵌入资源 .ui.json）===
    public virtual string GetDisplayJson();
    public virtual IReadOnlyList<UiElement> GetDisplayElements();

    // === IDisposable ===
    public void Dispose();
}
```

### 4.3 数据接收流程（基类）

```
硬件 → 串口
    ↓
SerialPortTransport.OnPortDataReceived
    → port.Read(buffer) → OnDataReceived(buffer)
    ↓
Sensor.OnTransportDataReceived(byte[] data)
    → for each byte: _rxBuf[_pWrite] = data[i]; _pWrite++
    ↓ (后台 DataProcessLoop 线程)
子类实现的 DataProcessLoop:
    → 扫描 _rxBuf 找帧头帧尾
    → 提取完整帧 → 解析测量值
    → OnMeasurementReceived(value) → MeasurementReceived 事件
```

### 4.4 具体实现：CurrentSensor500A

```csharp
// ATT.Protocol/Sensors/CurrentSensor500A.cs
[Display("500A Current Sensor", "500A range current sensor for arc fault detection",
         "Sensors", "Current")]
public class CurrentSensor500A : Sensor, IConfigurable, IDisplayable
{
    public override string Unit => "A";
    public double RatedRange => 500.0;

    // === 协议帧：ED..9E ===
    // [0xED][cmd][len][data...][0x9E]

    // === 命令字 ===
    // 0x01 GetFirmwareInfo    0x02 GetChannelInfo
    // 0x03 SetProcessMode     0x08 StartAcquisition
    // 0x09 StopAcquisition    0x0D WriteConfig
    // 0x0E ReadConfig         0x11 DataUpload
    // 0x13 AiArcReport

    // === DataProcessLoop（重写） ===
    // 扫描 _rxBuf 找 0xED 帧头
    // → 往后找 0x9E 帧尾
    // → 拷贝完整帧到 tmpBuf
    // → ProcessFrame(tmpBuf, frameLen)
    //   → CmdDataUpload → ParseWaveformData → OnMeasurementReceived(avg)
    //   → CmdAiArcReport → ParseArcResult → ArcDetectionResult

    // === IConfigurable ===
    public IReadOnlyList<ConfigurationParameter> Parameters { get; }
    // Actions: Get Firmware Info, Get Channel Info, Start Acquisition,
    //          Stop Acquisition, Read All Config
    // Settings: Sample Points (1024-65535), Sample Rate (1-100000 Hz),
    //           Sample Mode (Boolean, with arc tag)

    // === 发送命令（发送 ED..9E 帧）===
    // SendCommand([0xED, cmd, 0x00, 0x9E]) — 无数据命令
    // SendCommand([0xED, cmd, len, payload..., 0x9E]) — 带数据命令
}
```

---

## 5. CAN 桥接器（CanBridge）

### 5.1 ICanBridge 接口

```csharp
// ATT.Core/Interfaces/ICanBridge.cs
public interface ICanBridge
{
    string Name { get; }
    void Subscribe(ITransport transport);
    void SendCanFrame(CanFrame frame);
    event Action<CanFrame>? CanFrameReceived;

    void DataProcessLoop();
    void StartDataProcessThread();
    void StopDataProcessThread();
}
```

### 5.2 CanBridge 基类

```csharp
// ATT.Core/Base/CanBridge.cs
public abstract class CanBridge : ICanBridge, IDisposable
{
    // === 协议常量 ===
    protected const int RxBufferSize = 60000;
    protected const int MinFrameLength = 7;
    protected const byte FrameHeader1 = 0x55;
    protected const byte FrameHeader2 = 0xAA;
    protected const byte FrameTrailer = 0x5A;

    // === 命令字 ===
    private const byte CmdSendCanData = 0x10;
    private const byte CmdReceiveCanData = 0x11;

    // === 环缓冲（60000 字节）===
    protected readonly byte[] _rxBuf = new byte[RxBufferSize];
    protected int _pWrite, _pRead;

    // === 后台线程 ===
    protected Thread? _dataProcessThread;
    protected CancellationTokenSource? _cts;

    // === 构造 ===
    protected CanBridge();
    protected CanBridge(ITransport transport);  // 自动 Subscribe

    // === Subscribe ===
    public void Subscribe(ITransport transport);

    // === SendCanFrame（封装 0x55/0xAA 帧）===
    public void SendCanFrame(CanFrame frame);
    // 帧格式:
    // [0x55][0xAA][len][0x01][0x10][0x00][0x00][0x00][fmt][0x00][ID(2/4B)][dataLen][data...][XOR][0x5A]

    // 协议帧: 0x55 0xAA nLen 0x01 cmd ... XOR 0x5A
    // XOR = 字节[0..nLen-3] 的按位异或

    // === ProcessParsedFrame（子类可重写）===
    protected virtual void ProcessParsedFrame(byte[] frame, int length);

    // === HandleReceivedCanFrame（解析 CAN ID + 数据）===
    protected virtual void HandleReceivedCanFrame(byte[] frame);
    // → 解析 CAN ID (11/29-bit)、Data → CanFrame → CanFrameReceived

    // === DataProcessLoop（子类必须重写）===
    // 默认: while(!cancel) Thread.Sleep(50)
    public virtual void DataProcessLoop();

    // === 辅助 ===
    protected int GetBufferValidLength();
    protected static byte CalculateXor(byte[] buf, int? length = null);
    protected void SendRaw(byte[] data);  // 供子类发送自定义命令

    // === IDisposable ===
    public void Dispose();
}
```

### 5.3 具体实现：ZMCanBridge

```csharp
// ATT.Protocol/Bridges/ZMCanBridge.cs
public class ZMCanBridge : CanBridge
{
    // === 设备特定命令 ===
    // 0x00 CmdComCheck         — Ping
    // 0x01 CmdSetUartBaud      — 设置 UART 波特率
    // 0x02 CmdReadUartBaud     — 读取 UART 波特率
    // 0x03 CmdSetCanBaud       — 设置 CAN 波特率
    // 0x04 CmdReadCanBaud      — 读取 CAN 波特率
    // 0x0B CmdSetCanFrameFormat — 设置 CAN 帧格式

    public ZMCanBridge(ITransport transport) : base(transport);

    // === DataProcessLoop（重写）===
    public override void DataProcessLoop()
    {
        // 扫描 0x55/0xAA 帧头 → 读取 frameLen → TODO
        // → 校验 XOR → 验证 0x5A 帧尾
        // → ProcessParsedFrame(tmpBuf, frameLen)
    }

    // === 底层命令封装 ===
    private void ReadCmd(byte cmd);
    private void WriteCmd(byte cmd, byte[] payload);
    // 命令帧: [0x55][0xAA][7 + payload][0x01][cmd][data...][XOR][0x5A]

    // === 公开方法 ===
    public bool CheckCommunication();
    public void SetUartBaudRate(int baudRate);
    public void ReadUartBaudRate();
    public void SetCanBaudRate(CanBaudRateConfig config);
    public void ReadCanBaudRate();
    public void SetCanFrameFormat(CanFrameFormat format);
}
```

### 5.4 数据接收流程

```
硬件 → 串口
    ↓
SerialPortTransport.OnPortDataReceived
    → OnDataReceived(buffer)
    ↓
CanBridge.OnTransportDataReceived(byte[] data)
    → for each byte: _rxBuf[_pWrite] = data[i]; _pWrite++
    ↓ (后台 DataProcessLoop 线程)
ZMCanBridge.DataProcessLoop:
    → while (GetBufferValidLength() >= 7)
    →   if _rxBuf[_pRead] != 0x55 || next != 0xAA: _pRead++; continue
    →   frameLen = _rxBuf[_pRead + 2]
    →   校验 frameLen <= GetBufferValidLength()
    →   拷贝到 tmpBuf
    →   验证 tmpBuf[frameLen-1] == 0x5A
    →   如果 checkFlag != 0: 验证 XOR
    →   ProcessParsedFrame(tmpBuf, frameLen)
    →     → CmdReceiveCanData → HandleReceivedCanFrame
    →         → 解析 CAN ID、Data → CanFrameReceived(CanFrame)
```

---

## 6. 数据流

### 6.1 发送方向（Application → Hardware）

```
传感器发送配置命令:
  CurrentSensor500A.SetSampleRate(1000)
    → WriteConfig(0x01, 1000)
      → SendCmd(0x0D, [0x01, 0x00, 0x00, 0x03, 0xE8])
        → SendCommand([0xED, 0x0D, 0x05, 0x01, 0x00, 0x00, 0x03, 0xE8, 0x9E])
          → Transport.SendRaw(frame)

桥接器发送 CAN 帧:
  ZMCanBridge.SendCanFrame(CanFrame{Id=0x101, Data=[0xED, 0x08, 0x00, 0x9E]})
    → 封装 0x55/0xAA 帧
    → Transport.SendRaw(packedFrame)
```

### 6.2 接收方向（Hardware → Application）

```
传感器直连模式:
  硬件 → COM25: 0xED 0x11 0x04 0x00 [2048 ADC bytes] 0x9E
    ↓
  SerialPortTransport.OnDataReceived(byte[2052])
    ↓
  CurrentSensor500A.OnTransportDataReceived → _rxBuf
    ↓ DataProcessLoop 扫描
  找到 0xED + 0x9E 帧 → ProcessFrame
    → CmdDataUpload → ParseWaveformData
      → 16-bit ADC → 0-500A → avg = 325.5A
    → OnMeasurementReceived(325.5) → MeasurementReceived 事件

桥接器接收模式:
  硬件 → COM14: 0x55 0xAA 0x1A 0x01 0x11 [CAN frame data] XOR 0x5A
    ↓
  SerialPortTransport.OnDataReceived → _rxBuf
    ↓ DataProcessLoop 扫描
  找到 0x55/0xAA 帧 → XOR 校验
    → ProcessParsedFrame → HandleReceivedCanFrame
      → CanFrame{Id=0x101, Data=[0xED, 0x11, ...]}
    → CanFrameReceived(CanFrame) — 事件
```

### 6.3 完整数据流图

```
直连传感器:
  硬件 → Transport → OnTransportDataReceived → _rxBuf [环缓冲]
         ├──→ DataProcessLoop [后台线程]
         │      → 扫描帧结构 (ED..9E)
         │      → ProcessFrame
         │      → OnMeasurementReceived(value)
         │
发送: Sensor.SendCommand → Transport.SendRaw → 硬件

桥接器（独立使用）:
  硬件 → Transport → OnTransportDataReceived → _rxBuf [环缓冲，60KB]
         ├──→ DataProcessLoop [后台线程]
         │      → 扫描 0x55/0xAA 帧头
         │      → XOR 校验
         │      → ProcessParsedFrame → HandleReceivedCanFrame
         │      → CanFrameReceived(CanFrame) [事件]
```

---

## 7. UI / 配置层

### 7.1 IDisplayable — UI 控件描述

```csharp
// ATT.Core/Interfaces/IDisplayable.cs
public interface IDisplayable : IComponent
{
    string GetDisplayJson();
    IReadOnlyList<UiElement> GetDisplayElements();
}
```

**实现（Sensor 基类）：**
- `GetDisplayJson()` — 从嵌入式资源 `{FullTypeName}.ui.json` 读取
- `GetDisplayElements()` — 反序列化 JSON 中的 `"elements"` 数组

**UiElement 类型：**
- `Button` — 操作按钮（较零、复位、保存参数）
- `InputButton` — 输入框 + 发送按钮
- `Toggle` — 开关（开始/停止采集）
- `Display` — 只读数值/状态显示
- `Chart` — 波形显示区域
- `Group` — 容器（Expander 组）

### 7.2 IConfigurable — 运行时参数

```csharp
// ATT.Core/Interfaces/IConfigurable.cs
public interface IConfigurable : IComponent
{
    IReadOnlyList<ConfigurationParameter> Parameters { get; }
    void SetParameter(string name, object? value);
    object? GetParameter(string name);
    void InvokeAction(string name);
}
```

**CurrentSensor500A 实现的 Parameters：**

| Name | Type | Description |
|------|------|-------------|
| Get Firmware Info | Action | 查询固件版本 |
| Get Channel Info | Action | 查询通道配置 |
| Start Acquisition | Action | 开始连续采样 |
| Stop Acquisition | Action | 停止连续采样 |
| Read All Config | Action | 读取所有配置 |
| Sample Points | Integer (1024-65535) | 采样点数，默认 1024 |
| Sample Rate | Integer (1-100000) | 采样率 (Hz)，默认 1000 |
| Sample Mode | Boolean | 启用电弧标签 |

### 7.3 运行时输出

```csharp
// ATT.Cli/Models/RuntimeDeviceInfo.cs
public class RuntimeOutput
{
    public string Status { get; set; } = "connected";
    public List<RuntimeDevice> Devices { get; set; }
}

public class RuntimeDevice
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool Connected { get; set; }
    public string Unit { get; set; }
    public double ReadValue { get; set; }
    public string? DisplayJson { get; set; }
    public List<RuntimeParameter>? Parameters { get; set; }
}
```

UI 进程捕获 CLI 的 stdout JSON → 切换到运行时视图 → 根据 `displayJson` 渲染控件 → 根据 `parameters` 渲染配置面板。

---

## 8. 配置与启动

### 8.1 JSON 配置文件

```json
{
  "sensor": [
    {
      "name": "500A",
      "type": "CurrentSensor500A",
      "transport": { "type": "SerialPort", "portName": "COM25", "baudRate": 9375000 }
    }
  ],
  "bridge": [
    {
      "name": "CAN Bus COM14",
      "type": "ZMCanBridge",
      "transport": { "type": "SerialPort", "portName": "COM14", "baudRate": 1500000 },
      "canBaudRate": { "arbitrationBaudRate": 500, "dataBaudRate": 2 },
      "canFrameFormat": "Standard"
    }
  ]
}
```

### 8.2 DeviceService 启动流程

```csharp
// ATT.Cli/DeviceService.cs
public class DeviceService
{
    public IReadOnlyList<object> StartFromConfig(string configPath);

    // 1. 读取 JSON → DeviceConfigFile.Devices[]
    // 2. 根据 cfg.Type 路由:
    //    "ZMCanBridge"       → ParseBridge → CreateTransport → CreateZMBridge
    //    "CurrentSensor500A" → ParseSensor → CreateTransport
    //                           → 如果包含 Bridge: 先创建桥接器（共享 Transport）
    //                           → CreateCurrentSensor(name, transport)
    // 3. 打开 Transport
    // 4. 创建设备实例
    // 5. 注入 IConfigurable 参数
    // 6. 收集 RuntimeOutput → stdout

    private static ZMCanBridge CreateZMBridge(string name, ITransport transport, BridgeExtraConfig extra);
    // → new ZMCanBridge(transport)
    // → 配置 CAN 波特率、帧格式
    // → 订阅 CanFrameReceived 事件

    private static CurrentSensor500A CreateCurrentSensor(string name, ITransport transport);
    // → new CurrentSensor500A(transport)
    // → sensor.Subscribe(transport)
    // → 订阅 MeasurementReceived 事件
}
```

### 8.3 DeviceConfig 模型

```csharp
// ATT.Cli/Models/DeviceConfig.cs
public class DeviceConfig
{
    public string Name { get; set; }
    public string Type { get; set; }       // ZMCanBridge / CurrentSensor500A
    public TransportConfig? Transport { get; set; }
    public Dictionary<string, object?>? Parameters { get; set; }  // IConfigurable 参数
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; }  // type 专用配置
}

public class TransportConfig
{
    public string Type { get; set; }       // SerialPort / Tcp
    public string PortName { get; set; }
    public int BaudRate { get; set; }
    public string? Host { get; set; }
    public int? RemotePort { get; set; }
}

public class BridgeExtraConfig
{
    public TransportConfig Transport { get; set; }
    public CanBaudRateConfig? CanBaudRate { get; set; }
    public string? CanFrameFormat { get; set; }
}

public class SensorExtraConfig
{
    public TransportConfig? Transport { get; set; }       // 直连
    public BridgeExtraConfig? Bridge { get; set; }        // 通过桥接器
}
```

---

## 9. 已实现 vs 未实现

### 9.1 已实现的架构

| 组件 | 状态 | 说明 |
|------|------|------|
| `ITransport` / `Transport` | ✅ 完成 | 字节级收发，事件驱动 |
| `SerialPortTransport` | ✅ 完成 | System.IO.Ports |
| `TcpTransport` | ✅ 完成 | System.Net.Sockets |
| `ISensor` / `Sensor` | ✅ 完成 | 环缓冲 + 后台线程 + IDisplayable |
| `ICanBridge` / `CanBridge` | ✅ 完成 | 0x55/0xAA 帧同步 + XOR 校验 |
| `ZMCanBridge` | ✅ 完成 | 设备特定命令（波特率、帧格式） |
| `CurrentSensor500A` | ✅ 完成 | ED..9E 帧解析，500A 电流测量 |
| `IConfigurable` | ✅ 完成 | CurrentSensor500A 实现 |
| `IDisplayable` | ✅ 完成 | Sensor 基类提供嵌入资源支持 |
| `DeviceService` | ✅ 完成 | JSON 配置 → 设备创建 → 运行时输出 |

### 9.2 规划中 / 部分实现

| 组件 | 状态 | 说明 |
|------|------|------|
| `Sensor.Subscribe(ICanBridge)` | ⏳ 规划 | ISensor 接口已设计双模式，但 Sensor 基类尚未实现从桥接器获取数据；当前 DeviceService 中桥接器 + 传感器共享 Transport 后会各自独立解析，不通过 Subscribe(bridge) 链接 |
| `.ui.json` 嵌入资源 | ⏳ 待添加 | Sensor 基类的 IDisplayable 基础设施已就绪（自动从 `{FullTypeName}.ui.json` 读取），但 CurrentSensor500A 尚未添加该资源文件；当前运行时只能通过 IConfigurable Parameters 显示 |
| 桥接器模式连接 | ⏳ 部分 | DeviceService 已支持 sensor 配置中包含 bridge 引用，实现为共享同一个 Transport 实例，但尚未通过 Subscribe(bridge) 将传感器关联到桥接器 |

### 9.3 连接模式状态

| 模式 | 状态 | 路径 |
|------|------|------|
| 传感器直连串口 | ✅ 运行 | `Sensor(transport)` → `sensor.Subscribe(transport)` → 环缓冲 → DataProcessLoop |
| 桥接器独立使用（CAN 监控） | ✅ 运行 | `CanBridge(transport)` → `bridge.Subscribe(transport)` → 0x55/0xAA 解析 → CanFrameReceived |
| 传感器通过桥接器（CAN → 串口） | ⏳ 未完整 | 基础设施就绪，但缺少 `Sensor.Subscribe(ICanBridge)` 链接逻辑 |

---

## 附录：项目文件结构

```
ATT.Core/                           # 核心框架
├── Interfaces/
│   ├── IComponent.cs               # 标记接口
│   ├── ITransport.cs               # 传输层
│   ├── IInstrument.cs              # 仪器标记
│   ├── ISensor.cs                  # 传感器
│   ├── ICanBridge.cs               # CAN 桥接器
│   ├── IConfigurable.cs            # 可配置接口
│   ├── IDisplayable.cs             # UI 显示接口
│   └── ITestStep.cs                # 测试步骤
├── Base/
│   ├── Component.cs                # Name + Log
│   ├── Transport.cs                # Open/Close/SendRaw/events
│   ├── UartTransport.cs            # Transport + UART 配置
│   ├── Instrument.cs               # ITransport 注入
│   ├── Sensor.cs                   # 环缓冲 + 后台线程 + IDisplayable
│   ├── CanBridge.cs                # 0x55/0xAA 帧同步 + XOR 校验
│   └── TestStep.cs                 # 测试生命周期
├── Models/
│   ├── CanFrame.cs                 # CAN 数据帧
│   ├── CanFrameFormat.cs           # Standard/Extended
│   ├── CanBaudRateConfig.cs        # 波特率配置
│   ├── UartConfig.cs               # 串口配置
│   ├── ConfigurationParameter.cs   # 参数描述（ParameterType）
│   └── UiElement.cs               # UI 控件描述（UiElementType）
─────────────────────────────────────────────────
ATT.Protocol/                       # 具体实现
├── Bridges/
│   └── ZMCanBridge.cs              # 志明 CAN-UART 桥接器
├── Sensors/
│   └── CurrentSensor500A.cs        # 500A 电流传感器
└── Transports/
    ├── SerialPortTransport.cs       # System.IO.Ports
    └── TcpTransport.cs             # System.Net.Sockets
─────────────────────────────────────────────────
ATT.Cli/                            # CLI 前端
├── Program.cs                      # 命令行入口
├── DeviceService.cs                # 配置加载 + 设备创建 + 运行时输出
├── Parsers/DataParser.cs           # 数据解析（CSV/空格/逗号）
├── Analyzers/FeatureAnalyzer.cs    # 统计分析（min/max/mean/stddev）
└── Models/
    ├── DeviceConfig.cs             # 设备配置（DeviceConfig、TransportConfig 等）
    ├── RuntimeDeviceInfo.cs        # 运行时输出（RuntimeOutput、RuntimeDevice）
    ├── PacketData.cs               # 数据包模型
    └── AnalysisResult.cs           # 分析结果模型
─────────────────────────────────────────────────
config/
└── bridge-com14.json               # 示例配置文件
```
