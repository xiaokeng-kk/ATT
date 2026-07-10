# 运行时命令接收机制设计

> 参考 OpenTAP（Keysight）的 Session / Engine 架构
> 创建时间：2026-07-10

## 概述

ATT 目前仅有 `ITestStep` 的生命周期控制（`PrePlanRun → Run → PostPlanRun`），
缺乏运行时的外部命令接收能力。本文档描述 OpenTAP 的方案，并规划 ATT 的实现路径。

---

## 一、OpenTAP 的方案

### 1. Session + Engine 架构

```
CLI ──┐
      ├── Socket/管道 ──→ Engine ──→ Session ──→ TestPlan ──→ TestStep[]
GUI ──┘
```

- **Engine**：核心进程，管理 Session 生命周期，监听外部命令通道
- **Session**：单次 TestPlan 运行的上下文，持有状态和控制标志
- **TestPlan**：步骤容器，遍历执行 ChildSteps

### 2. 运行时状态机

```
Idle → Running ⇄ Paused → [Completed | Aborted | Failed]
              ↑                  ↘ Error
              └─── Resume ────────┘
```

- `Pause()`：等当前步骤执行完再暂停
- `Stop()`：等当前步骤执行完，不再执行后续步骤
- `Abort()`：立即终止（通过 CancellationToken）

### 3. Step 内部响应机制

TestStep.Run() 内部需要主动配合：

```csharp
public override void Run()
{
    for (int i = 0; i < totalCycles; i++)
    {
        // 检查外部命令
        if (Session.IsAborting) break;
        if (Session.IsPaused) Session.WaitForResume();

        // 执行本周期逻辑
        DoOneCycle(i);
    }
}
```

### 4. 外部命令通道

OpenTAP GUI 与 Engine 通过 TCP socket 通信，协议示例：

```
→ PAUSE\n
← OK\n
→ RESUME\n
← OK\n
→ ABORT\n
← OK\n
```

Engine 在独立线程接收命令，设置 Session 状态标志，不阻塞步骤执行。

---

## 二、ATT 实现方案

### 1. EngineState 枚举

```csharp
public enum EngineState
{
    Idle,
    Running,
    Pausing,    // 请求暂停，等当前步骤完成
    Paused,
    Resuming,
    Stopping,   // 请求停止，等当前步骤完成
    Aborting,   // 立即终止
    Completed,
    Failed
}
```

### 2. Session 类（新增）

```csharp
public class Session
{
    public EngineState State { get; private set; }
    public CancellationToken Token => _cts.Token;

    // 控制方法
    public void Pause();
    public void Resume();
    public void Stop();
    public void Abort();

    // 步骤内部调用 — 阻塞直到 Resume
    public void WaitWhilePaused();
}
```

### 3. TestStep 配合

基类 `TestStep` 新增属性：

```csharp
public Session? Session { get; set; }

// 在 Run() 中间隔检查的辅助方法
protected void CheckAbort()
{
    if (Session?.Token.IsCancellationRequested == true)
        throw new OperationCanceledException();
}

protected void WaitWhilePaused()
{
    Session?.WaitWhilePaused();
}
```

步骤编写模式：

```csharp
public override void Run()
{
    for (int i = 0; i < 100; i++)
    {
        WaitWhilePaused();   // 响应暂停
        CheckAbort();        // 响应终止

        // 实际工作
        Measure(i);
    }
}
```

### 4. Engine + 命令通道

```csharp
public class Engine
{
    private readonly TcpListener _listener;  // 或 NamedPipe
    private Session? _currentSession;

    public async Task RunTestPlan(TestPlan plan)
    {
        _currentSession = new Session();
        await Task.Run(() => plan.Execute(_currentSession));
    }

    // 在独立线程中接收命令
    private void CommandLoop()
    {
        while (true)
        {
            var cmd = _reader.ReadLine();
            switch (cmd)
            {
                case "PAUSE":  _currentSession?.Pause();  break;
                case "RESUME": _currentSession?.Resume(); break;
                case "ABORT":  _currentSession?.Abort();  break;
            }
        }
    }
}
```

### 5. TestPlan 遍历执行

```csharp
public class TestPlan
{
    public List<ITestStep> Steps { get; set; }

    public void Execute(Session session)
    {
        foreach (var step in Steps)
        {
            if (session.State == EngineState.Stopping ||
                session.State == EngineState.Aborting)
                break;

            step.Session = session;

            step.PrePlanRun();
            if (session.State == EngineState.Aborting) break;

            step.Run();
            if (session.State == EngineState.Aborting) break;

            step.PostPlanRun();
        }

        session.State = EngineState.Completed;
    }
}
```

---

## 三、命令通道协议

### 3.1 字符串协议（适用于调试 / CLI）

```
请求 → PAUSE\n
响应 → OK\n
      ERR <reason>\n

请求 → RESUME\n
请求 → STOP\n
请求 → ABORT\n
请求 → STATE\n
响应 → STATE Running\n
```

### 3.2 JSON 协议（适用于 GUI）

```json
→ {"cmd": "pause"}
← {"ok": true}
→ {"cmd": "abort"}
← {"ok": true}
→ {"cmd": "state"}
← {"state": "Running", "step": "MeasureVoltage", "verdict": "Pass"}
```

---

## 四、实施路线

| 阶段 | 内容 | 涉及文件 |
|------|------|----------|
| 1 | 新增 `EngineState` 枚举 | `src/ATT.Core/EngineState.cs` |
| 2 | 新增 `Session` 类 | `src/ATT.Core/Session.cs` |
| 3 | `TestStep` 添加 `Session` 属性 + 辅助方法 | `src/ATT.Core/Base/TestStep.cs` |
| 4 | 新增 `TestPlan` 容器类 | `src/ATT.Core/TestPlan.cs` |
| 5 | 新增 `Engine` 类 + TCP 命令通道 | `src/ATT.Core/Engine.cs` |
| 6 | CLI 支持发送命令（`--pause` / `--abort`） | `src/PacketAnalyzer/Program.cs` |
| 7 | 集成测试 | `tests/` |

---

## 五、备注

- 暂停/停止是"优雅"控制——等当前步骤完成再响应
- Abort 是"强制"控制——通过 CancellationToken 抛出异常立即退出
- 步骤需要**主动配合**调用 `WaitWhilePaused()` / `CheckAbort()`，框架无法强制中断
- 此模式与 OpenTAP 的 `Session` + `ResourceManager` 方案一致
