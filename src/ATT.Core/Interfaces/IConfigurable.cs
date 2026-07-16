using ATT.Core.Models;

namespace ATT.Core.Interfaces;

/// <summary>
/// Configurable component interface.
/// Components implement this to declare what parameters / actions they expose
/// so the UI layer can dynamically generate controls.
/// </summary>
public interface IConfigurable : IComponent
{
    /// <summary>
    /// Returns the list of configurable parameters exposed to the UI.
    /// Each parameter describes its type, range, options, etc.
    /// </summary>
    IReadOnlyList<ConfigurationParameter> Parameters { get; }

    /// <summary>
    /// Apply a parameter value at runtime.
    /// </summary>
    void SetParameter(string name, object? value);

    /// <summary>
    /// Get the current value of a parameter.
    /// </summary>
    object? GetParameter(string name);

    /// <summary>
    /// Invoke an action parameter (button click).
    /// </summary>
    void InvokeAction(string name);
}
