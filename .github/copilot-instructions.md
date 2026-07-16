# ATT (Automated Test Toolkit) — Copilot Instructions

## Build Commands

```bash
# Build entire solution
dotnet build PacketAnalyzer.sln

# Build single project
dotnet build src/ATT.Core/ATT.Core.csproj

# Run the ATT.Cli tool
dotnet run --project ATT.Cli/ATT.Cli.csproj -- --help
dotnet run --project ATT.Cli/ATT.Cli.csproj -- --data "1.5 2.3 4.1"
dotnet run --project ATT.Cli/ATT.Cli.csproj -- --file data.csv --csv
```

## Architecture

Three-layer design inspired by OpenTAP:

```
ATT.Cli/                CLI tool (standalone, no ATT.Core dependency)
ATT.Protocol/            Domain-specific CAN-UART protocol (references ATT.Core)
ATT.Core/                Core framework — interfaces, base classes, catalog
```

Skill projects (new classlibs) reference `ATT.Core` and optionally `ATT.Protocol`, then get discovered by `ComponentCatalog.ScanDirectory()`.

## Interface Layering (ATT.Core)

按通信接口和仪器类型两条主线分类：

```
IComponent                  — Marker interface, all discoverable components implement this
  │
  ├── ITransport            — 通信接口：Open()/Close()/IsConnected/SendRaw/ConnectionStateChanged
  │     ├── SerialPortTransport     串口通信
  │     ├── TcpTransport            TCP/IP 通信
  │     ├── HidTransport            HID 通信
  │     └── ...                     其他通信方式
  │
  ├── IInstrument           — 仪器接口（标记接口，依赖 ITransport 收发数据）
  │     └── Instrument (abstract) — 仪器虚类，构造函数注入 ITransport
  │             ├── Oscilloscope        示波器
  │             ├── PowerSupply         电源
  │             ├── Multimeter          万用表
  │             └── ...                 其他仪器
  │
  └── ITestStep             — 测试步骤：PrePlanRun() → Run() → PostPlanRun()
```

```
ICanBridge (ATT.Protocol) — CAN 桥接器，PC 连接 CAN 节点的通用接口
  └── CanBridge                   — 实现类，组合 ITransport 进行协议打包收发
```

**核心设计原则**：Instrument 内部不直接操作硬件端口，通过 `ITransport` 收发数据。不同的通信方式（串口/TCP/HID）实现各自的 `ITransport`，构造时注入到 Instrument：

```csharp
public abstract class Instrument : IInstrument
{
    protected readonly ITransport Transport;

    protected Instrument(ITransport transport)
    {
        Transport = transport;
    }

    // 收数据：Transport.SendRaw(...)
    // 发数据：Transport 的 ConnectionStateChanged / 派生类主动 Read
}
```

这种设计 = scopehal 的 `SCPITransport → SCPIDevice + Instrument` 分离模式。

## Base Classes (ATT.Core)

- **Component** (abstract) — Implements IComponent, provides `Name`, `TraceSource Log`.
- **Transport** (abstract) — Extends Component, implements ITransport. Has `IsConnected`, `ConnectionStateChanged`, `DataReceived` and abstract `Open/Close/SendRaw`. Subclasses: `SerialPortTransport`, `TcpTransport`, etc.
- **Instrument** (abstract) — Extends Component, adds `Identity` property. Constructor takes `ITransport transport` (composition). All I/O via `Transport.SendRaw()`.
- **TestStep** (abstract) — Full ITestStep lifecycle. Verdict setter auto-propagates to parent via `OnChildVerdictChanged`.
- **TestStep** (abstract) — Full ITestStep lifecycle. Verdict setter auto-propagates to parent via `OnChildVerdictChanged`. Steps enabled by default.

## Key Conventions

- **ITestStep lifecycle**: `PrePlanRun()` (forward order) → `Run()` (abstract) → `PostPlanRun()` (reverse order)
- **Verdict enum**: `None → Pass → Fail → Error → Inconclusive`. Steps propagate their verdict to parents automatically.
- **ComponentCatalog**: runtime component discovery. `ScanDirectory("output")` loads all DLLs and registers `IComponent` implementations. `GetComponents<T>()` filters by type.
- **Skill projects** (new feature domains): create as `dotnet new classlib`, add project reference to `ATT.Core` (and optionally `ATT.Protocol`). Implement `ITestStep` or extend base classes.
- **DisplayAttribute**: `[Display("Name", "Description", "Group1", "Group2")]` with optional `Order = n`. Used on classes, properties, and interfaces for UI metadata.
- **UnitAttribute**: `[Unit("V", PreScaling = 1000)]` for physical quantity annotations on properties.
- **All projects use .NET 9**, `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`.
- **Dependency chain**: `ATT.Protocol → ATT.Core`. `ATT.Cli` is standalone (no Core ref). Skill projects reference `ATT.Core` at minimum.

## CAN-UART Protocol (ATT.Protocol)

**Frame format**: `[0x55][0xAA][nLen][checkFlag][cmd][...data...][XOR][0x5A]`
- `checkFlag = 0` disables XOR validation, `checkFlag = 0x01` enables it
- XOR covers bytes `[0, nLen-3]`, min frame length = 7 bytes

**Commands**: `0x00` (ping), `0x01` (set UART baud), `0x02` (read), `0x03` (set CAN baud), `0x04` (read), `0x0B` (set frame format), `0x10` (CAN send), `0x11` (CAN receive)

**Key interfaces**:
- `ICanBridge` — 纯 CAN 语义：`SendCanFrame(CanFrame)` / `CanFrameReceived` 事件
- `CanBridge` (abstract) — 虚类，提供协议帧打包/解析/环形缓冲区/后台解析线程等公共基础设施
- `ZhiMingCanBridge` — 继承 CanBridge，添加志明 CAN 转换器特定的配置命令（波特率、帧格式等）
- **使用方式**：用户自行管理 Transport 的 Open/Close，Bridge 只负责协议编解码

**Models**: `CanFrame` (Id, FrameFormat, Data), `CanFrameFormat` (Standard/Extended), `CanBaudRateConfig` (ArbitrationBaudRate, DataBaudRate), `UartConfig` (PortName, BaudRate, TimeoutMs)

## ATT.Cli CLI

- Entry point: `Program.cs` with top-level statements, `ParseArgs()` static local function
- Args: `--file/-f <path>`, `--data/-d <values>`, `--csv/-c`, `--help/-h`
- Falls back to stdin (pipe input) if no args given
- Parser: `DataParser.Parse()` — handles CSV/space/comma/semicolon delimiters, `#` comment lines
- Analysis: `FeatureAnalyzer.Analyze()` computes min, max, peak-to-peak, mean, variance, stddev

## Reference: OpenTAP Patterns (Architecture Inspiration)

ATT's patterns are inspired by OpenTAP (Keysight). Key reference patterns:
- **Plugin discovery**: `PluginManager` scans assemblies via metadata-only PEReader, lazy `Assembly.LoadFrom` on access. ATT uses `ComponentCatalog` (simpler approach).
- **Package.xml**: auto-generated via MSBuild `AfterTargets="Build"` target → `tap package create package.xml`. Not yet implemented in ATT.
- **SCPI instrument layering**: `SCPITransport → SCPIDevice → SCPIInstrument → SCPIOscilloscope → RigolOscilloscope`. The transport/device/instrument separation maps to ATT's `IResource/IProtocolBridge/IInstrument`.
