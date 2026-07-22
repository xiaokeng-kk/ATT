using ATT.Core.Models;

namespace ATT.Core.Interfaces;

/// <summary>
/// Displayable component interface.
/// Components implement this to declare what UI elements (buttons, displays,
/// charts, etc.) they want the frontend to render. The description can come
/// from an embedded JSON resource or be generated at runtime.
/// </summary>
public interface IDisplayable : IComponent
{
    /// <summary>
    /// Returns a JSON string describing the UI elements for the frontend.
    /// Default implementation (in Sensor base class) reads from embedded
    /// resource file "{FullTypeName}.ui.json".
    /// Override to generate dynamically at runtime.
    /// </summary>
    string GetDisplayJson();

    /// <summary>
    /// Returns the parsed list of UI elements.
    /// Default implementation deserializes GetDisplayJson().
    /// Override to build the list programmatically.
    /// </summary>
    IReadOnlyList<UiElement> GetDisplayElements();
}
