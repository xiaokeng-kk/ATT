namespace ATT.UI.Host.ViewModels;

/// <summary>
/// Represents a COM port with its device description.
/// Displayed in the UI as "Description (PortName)".
/// </summary>
public class ComPortInfo
{
    /// <summary>Port name, e.g. "COM3"</summary>
    public required string PortName { get; init; }

    /// <summary>Device description, e.g. "Prolific USB-to-Serial Comm Port"</summary>
    public required string Description { get; init; }

    /// <summary>
    /// Display string used by the ComboBox.
    /// Falls back to just the port name if description is empty.
    /// </summary>
    public override string ToString()
    {
        return string.IsNullOrEmpty(Description)
            ? PortName
            : $"{Description} ({PortName})";
    }
}
