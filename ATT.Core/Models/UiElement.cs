namespace ATT.Core.Models;

/// <summary>
/// Type of a UI display element — determines which Avalonia control to render.
/// </summary>
public enum UiElementType
{
    /// <summary>Action button (zero calibration, reset, save params, etc.)</summary>
    Button,

    /// <summary>Text input field + Send button composite control</summary>
    InputButton,

    /// <summary>Toggle switch (start/stop acquisition, enable/disable mode)</summary>
    Toggle,

    /// <summary>Read-only numeric/status display bound to a sensor property</summary>
    Display,

    /// <summary>Chart / waveform display area (placeholder for now)</summary>
    Chart,

    /// <summary>Group container wrapping child elements (Expander)</summary>
    Group,
}

/// <summary>
/// Describes a single UI element to be rendered by the frontend.
/// Sensors create instances of this via JSON deserialization or code.
/// </summary>
public class UiElement
{
    /// <summary>Unique identifier within this sensor's element set</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Control type — determines the template used for rendering</summary>
    public UiElementType Type { get; init; } = UiElementType.Button;

    /// <summary>Display label shown in the UI</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Tooltip / description text</summary>
    public string? Description { get; init; }

    /// <summary>
    /// For Button/InputButton: name of the action method to invoke via IConfigurable.InvokeAction().
    /// For Toggle: name of the action for "on" state (StartAcquisition).
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// For Toggle: name of the action for "off" state (StopAcquisition).
    /// If null, toggling sends Action for on, ActionOff for off.
    /// </summary>
    public string? ActionOff { get; init; }

    /// <summary>
    /// For Display/Chart: property or method name on the sensor to bind/read
    /// (e.g. "ReadValue", "ArcDetected", "CurrentWaveform").
    /// </summary>
    public string? Bind { get; init; }

    /// <summary>Engineering unit for Display elements (e.g. "A", "V", "°C")</summary>
    public string? Unit { get; init; }

    /// <summary>Child elements — used when Type == Group</summary>
    public List<UiElement> Children { get; init; } = [];

    /// <summary>
    /// Extended properties for type-specific configuration:
    /// - "chartType": "line" | "bar"
    /// - "inputPlaceholder": placeholder text for InputButton text box
    /// - "buttonLabel": custom label for InputButton's send button
    /// - "inputType": "text" | "hex" | "number"
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = [];

    /// <summary>Sort order within the parent group / container</summary>
    public double Order { get; init; }
}
