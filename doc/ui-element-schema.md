# UI Element JSON Schema

This document describes the JSON schema used by `IDisplayable` components to declare
frontend UI elements. Each sensor can embed a `{FullTypeName}.ui.json` resource file
or override `GetDisplayJson()` to provide this schema at runtime.

## Schema Structure

```json
{
  "$schema": "att-ui-schema.json",
  "version": "1.0",
  "elements": [
    {
      "id": "unique-element-id",
      "type": "button | inputButton | toggle | display | chart | group",
      "label": "Display Label",
      "description": "Tooltip text",
      "action": "InvokeActionName",
      "actionOff": "StopActionName",
      "bind": "PropertyOrMethodName",
      "unit": "A | V | °C | ...",
      "order": 0,
      "properties": {
        "chartType": "line",
        "inputPlaceholder": "Enter hex data...",
        "buttonLabel": "Send",
        "inputType": "text | hex | number"
      },
      "children": [ ... ]
    }
  ]
}
```

## Element Types

### Button
A clickable action button.

| Field | Description |
|-------|-------------|
| `type` | `"button"` |
| `label` | Button text |
| `action` | Name passed to `IConfigurable.InvokeAction()` |
| `description` | Tooltip |

### InputButton
A text input field with a send button to the right.

| Field | Description |
|-------|-------------|
| `type` | `"inputButton"` |
| `label` | Button label text |
| `action` | Action name invoked with input text as parameter |
| `description` | Placeholder text in the input field |
| `properties.inputPlaceholder` | Placeholder text |
| `properties.buttonLabel` | Send button label (default: "发送") |
| `properties.inputType` | `"text"`, `"hex"`, or `"number"` |

### Toggle
An on/off toggle switch.

| Field | Description |
|-------|-------------|
| `type` | `"toggle"` |
| `label` | Toggle label |
| `action` | Action invoked when toggled ON |
| `actionOff` | Action invoked when toggled OFF |
| `bind` | Property name for current on/off state |

### Display
A read-only value display (numeric, status, etc.).

| Field | Description |
|-------|-------------|
| `type` | `"display"` |
| `label` | Label text |
| `bind` | Property/method name to read the value from |
| `unit` | Engineering unit suffix |

### Chart
A chart/graph area (reserved for future implementation).

| Field | Description |
|-------|-------------|
| `type` | `"chart"` |
| `label` | Chart title |
| `bind` | Data source property name |
| `properties.chartType` | `"line"` (default) or `"bar"` |

### Group
A collapsible group container for child elements.

| Field | Description |
|-------|-------------|
| `type` | `"group"` |
| `label` | Group header text |
| `children` | Array of nested UiElement objects |
| `properties.expanded` | `true` (default) or `false` |

## Example: CurrentSensor500A

```json
{
  "$schema": "att-ui-schema.json",
  "version": "1.0",
  "elements": [
    {
      "id": "acquisition-group",
      "type": "group",
      "label": "采集控制",
      "order": 0,
      "children": [
        {
          "id": "acquisition-toggle",
          "type": "toggle",
          "label": "数据采集",
          "description": "启动/停止连续电流采样",
          "action": "Start Acquisition",
          "actionOff": "Stop Acquisition",
          "order": 0
        },
        {
          "id": "zero-calibration",
          "type": "button",
          "label": "较零",
          "description": "执行传感器较零操作",
          "action": "Zero Calibration",
          "order": 1
        },
        {
          "id": "reset",
          "type": "button",
          "label": "复位",
          "description": "复位传感器",
          "action": "Reset",
          "order": 2
        },
        {
          "id": "save-params",
          "type": "button",
          "label": "保存参数",
          "description": "保存当前配置参数到设备",
          "action": "Save Parameters",
          "order": 3
        }
      ]
    },
    {
      "id": "send-data",
      "type": "inputButton",
      "label": "发送",
      "description": "输入十六进制数据发送到传感器",
      "action": "Send Custom Data",
      "properties": {
        "inputPlaceholder": "FF 01 00 00",
        "inputType": "hex"
      },
      "order": 1
    },
    {
      "id": "display-group",
      "type": "group",
      "label": "数据显示",
      "order": 2,
      "children": [
        {
          "id": "current-value",
          "type": "display",
          "label": "当前电流",
          "bind": "ReadValue",
          "description": "实时电流测量值",
          "order": 0
        },
        {
          "id": "arc-status",
          "type": "display",
          "label": "电弧状态",
          "bind": "ArcDetected",
          "description": "AI 电弧检测结果",
          "order": 1
        }
      ]
    },
    {
      "id": "waveform-chart",
      "type": "chart",
      "label": "电流波形",
      "bind": "CurrentWaveform",
      "properties": {
        "chartType": "line"
      },
      "order": 3
    }
  ]
}
```
