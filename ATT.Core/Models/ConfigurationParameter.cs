namespace ATT.Core.Models;

/// <summary>
/// Type of a configuration parameter — determines the UI control to render.
/// </summary>
public enum ParameterType
{
    /// <summary>Action command — rendered as a Button, no input needed</summary>
    Action,

    /// <summary>Integer parameter — rendered as a numeric text box</summary>
    Integer,

    /// <summary>Floating-point parameter — rendered as a numeric text box</summary>
    Double,

    /// <summary>Boolean parameter — rendered as a CheckBox</summary>
    Boolean,

    /// <summary>Enum parameter — rendered as a ComboBox</summary>
    Enum,

    /// <summary>String parameter — rendered as a TextBox</summary>
    String,
}

/// <summary>
/// Describes a single configurable parameter on a component.
/// The UI layer reads these to dynamically generate controls.
/// </summary>
public class ConfigurationParameter
{
    /// <summary>Display name shown in the UI</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Description / tooltip</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Determines which UI control to render</summary>
    public ParameterType ParameterType { get; init; } = ParameterType.Action;

    /// <summary>Default value</summary>
    public object? DefaultValue { get; init; }

    /// <summary>Current runtime value</summary>
    public object? CurrentValue { get; set; }

    /// <summary>For Enum type: available options as (display name, value) pairs</summary>
    public IReadOnlyList<(string Name, object Value)>? EnumOptions { get; init; }

    /// <summary>For numeric types: minimum value</summary>
    public double? MinValue { get; init; }

    /// <summary>For numeric types: maximum value</summary>
    public double? MaxValue { get; init; }

    /// <summary>Group path, e.g. ["Sensor", "Sampling"]</summary>
    public string[] Groups { get; init; } = [];

    /// <summary>Sort order within the group</summary>
    public double Order { get; init; }
}
