# ATT 自动化测试平台软件架构设计

> 创建时间：2026-07-10

---

## 1. 总体架构

系统采用 **Engine 核心 + 多客户端 + 插件化设备驱动** 架构。

核心思想：

- Engine 负责业务逻辑、设备管理、测试流程执行
- GUI、CLI、脚本都是 Engine 的控制入口
- DLL 插件负责提供具体设备能力
- 配置文件负责描述系统组成和参数

整体结构：

```
                 +----------------+
                 | Avalonia GUI   |
                 | 用户界面       |
                 +----------------+
                         |
                         |
                 HTTP / SignalR / IPC
                         |
                         |
+------------------------------------------------+
|              ATT.Engine (Backend)              |
|              系统核心运行引擎                  |
|                                                |
|  +---------------+  +----------------------+   |
|  | ConfigManager |  | PluginManager        |   |
|  +---------------+  +----------------------+   |
|                                                |
|  +---------------+  +----------------------+   |
|  | DeviceManager |  | TestEngine           |   |
|  +---------------+  +----------------------+   |
|                                                |
|  +---------------+  +----------------------+   |
|  | ScriptEngine  |  | ResourceManager      |   |
|  +---------------+  +----------------------+   |
+------------------------------------------------+
              |
              |
       Plugin Interface
              |
              |
+-------------+-------------+-------------+
|                           |             |
v                           v             v

CAN Bridge DLL          UART DLL      USB DLL

Sensor DLL              Scope DLL    Instrument DLL

              |
              |
          Hardware
```

---

## 2. ATT.Engine（Backend）

### 2.1 定义

Backend 是系统核心进程。

负责：

- 加载插件
- 管理设备生命周期
- 执行测试流程
- 管理配置
- 维护系统状态
- 提供 API 给 GUI / CLI / Script

运行形式：

```
ATT.Engine.exe
```

可以长期运行：

```
启动
 |
加载配置
 |
加载插件
 |
等待命令
 |
执行测试
 |
持续运行
```

---

## 3. CLI

### 3.1 定义

CLI 是命令行客户端。

职责：

- 接收用户命令
- 转换成 Engine 请求
- 显示执行结果

CLI 不直接访问硬件。

推荐方式：

```
ATT.CLI.exe

       |
       |
 API / IPC

       |
       |

ATT.Engine.exe

       |
       |
Plugin DLL

       |
       |
Hardware
```

### 3.2 示例

启动 Engine：

```
ATT.Engine.exe config.json
```

查询状态：

```
ATT.CLI.exe status
```

输出：

```
CAN Bridge:
    Connected

Sensor:
    P

RX Count:
    123456
```

修改参数：

```
ATT.CLI.exe config set baudrate 500000
```

实际流程：

```
CLI
 |
 |
Config API
 |
 |
Engine
 |
 |
ConfigManager
 |
 |
通知 GUI
```

---

## 4. GUI（Avalonia）

### 4.1 定义

GUI 只负责：

- 显示状态
- 用户操作
- 参数编辑

GUI 不直接操作硬件。

结构：

```
Avalonia View
      |
ViewModel
      |
Service Client
      |
ATT.Engine
```

### 4.2 状态同步

Engine 是唯一状态源。

例如设备状态：

```json
{
    "Connected": true,
    "RxCount": 12345
}
```

状态变化：

```
CAN收到数据
      |
DeviceManager更新状态
      |
发布事件
      |
SignalR/WebSocket
      |
GUI ViewModel
      |
界面刷新
```

---

## 5. Plugin DLL（插件）

### 5.1 定义

插件负责实现具体能力。例如：

```
Plugins
 |
 +-- ATT.Plugin.CAN.dll
 |
 +-- ATT.Plugin.UART.dll
 |
 +-- ATT.Plugin.P.dll
 |
 +-- ATT.Plugin.Scope.dll
```

Engine 不关心具体实现。

### 5.2 插件接口

```csharp
public interface IResource
{
    string Name { get; }
    void Open();
    void Close();
    bool IsConnected { get; }
}
```

CAN Bridge 示例：

```csharp
public class CanBridge : IResource
{
    public void Open() { /* ... */ }
    public void Close() { /* ... */ }
}
```

P 示例：

```csharp
public class P : IResource
{
    // 实现 IResource
}
```

---

## 6. ResourceManager

负责管理资源生命周期。

配置示例：

```json
{
    "Bridge": "CAN0",
    "Devices": [
        {
            "Type": "P",
            "Address": 16
        }
    ]
}
```

加载流程：

```
ResourceManager
        |
创建 CAN Bridge
        |
创建 P
        |
绑定关系
```

形成资源树：

```
CAN Bridge
      |
      |
P Sensor
```

---

## 7. ConfigManager

负责：

- 加载配置
- 保存配置
- 修改配置
- 发布配置变化

配置文件：`config.json`

```json
{
    "CAN": {
        "Baudrate": 500000
    }
}
```

修改流程：

```
CLI
 |
Engine
 |
ConfigManager
 |
保存JSON
 |
发送ConfigChanged事件
 |
GUI刷新
```

---

## 8. ScriptEngine

负责执行复杂逻辑。

配置文件适合描述：

- 设备是什么
- 参数是多少

脚本适合描述：

- 怎么测试
- 怎么判断
- 失败怎么办

示例：

```csharp
device.Connect();
device.SetCurrent(10);
var value = device.Measure();

if (value > 30)
{
    Log.Error("Fail");
}
```

支持语言：

- C# Script
- Python Script
- Lua Script

---

## 9. TestEngine

负责测试流程执行。

示例流程：

```
TestPlan
 |
 +-- Connect Device
 |
 +-- Calibration
 |
 +-- Measure
 |
 +-- Verify
 |
 +-- Report
```

---

## 10. 数据流

### 10.1 控制流

```
GUI

CLI

Script
      |
      |
ATT.Engine
      |
      |
DeviceManager
      |
      |
Plugin DLL
      |
      |
Hardware
```

### 10.2 状态流

```
Hardware
      |
Plugin
      |
Engine State
      |
Event Bus
      |
GUI / Logger / Database
```

---

## 11. 推荐工程结构

```
ATT.sln
 |
+-- ATT.Engine
|   +-- DeviceManager
|   +-- ConfigManager
|   +-- TestEngine
|   +-- ScriptEngine
|
+-- ATT.GUI
|   +-- Avalonia
|   +-- MVVM
|
+-- ATT.CLI
|
+-- ATT.Core
|   +-- Interfaces
|
+-- ATT.Plugin.CAN
|
+-- ATT.Plugin.UART
|
+-- ATT.Plugin.USB
|
+-- ATT.Plugin.Sensor
|
+-- ATT.Plugin.Scope
```

---

## 12. 设计原则总结

### Engine 是核心

所有操作汇聚到 Engine：

```
GUI
CLI
Script
Remote API
       |
       v
ATT.Engine
```

### DLL 是能力扩展

新增设备只需增加 DLL，无需修改 Engine。

### 配置描述系统

JSON 描述：

- 设备
- 参数
- 连接关系

### 脚本描述流程

Script 描述：

- 测试逻辑
- 判断逻辑
- 自动化流程

### GUI 只是客户端

不保存真实状态，真实状态在 `ATT.Engine`。

---

最终系统类似 OpenTAP 的插件化思想，但采用现代 .NET 架构：

```
Engine + Plugin + Script + GUI + CLI
```

支持能力：

- 单机测试
- 自动化测试
- 多设备管理
- 远程控制
- 长时间运行
- 自定义扩展

---

## 后续规划

后续可在当前基础上继续设计：

- **接口层**：IResource / IInstrument / IBridge
- **插件加载机制**：ComponentCatalog 增强
- **消息总线**：Engine 事件发布/订阅
- **测试流程格式**：XML / JSON TestPlan 序列化
