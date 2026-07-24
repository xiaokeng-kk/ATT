using ATT.Core.Models;

namespace ATT.Core.Interfaces;

/// <summary>
/// Displayable component interface.
/// Components implement this to declare what UI elements (buttons, displays,
/// charts, etc.) they want the frontend to render.
/// Each component generates its UI description at runtime via GetDisplayJson().
/// </summary>
public interface IDisplayable : IComponent
{
    /// <summary>
    /// Returns a JSON string describing the UI elements for the frontend.
    /// Components override this to dynamically generate UI descriptions
    /// based on their current state and parameters.
    /// </summary>
    string GetDisplayJson();

    /// <summary>
    /// Returns the parsed list of UI elements.
    /// Default implementation deserializes GetDisplayJson().
    /// Override to build the list programmatically.
    /// </summary>
    IReadOnlyList<UiElement> GetDisplayElements();
}
