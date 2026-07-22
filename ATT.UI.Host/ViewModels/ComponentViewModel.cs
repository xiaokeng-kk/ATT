using System.Collections.ObjectModel;
using ATT.Core.Interfaces;
using ATT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// ViewModel wrapping an IConfigurable component.
/// Exposes its parameters as an observable collection for dynamic UI binding.
/// </summary>
public partial class ComponentViewModel : ObservableObject
{
    private readonly IDisplayable? _displayable;
    private IDisposable? _refreshTimer;

    [ObservableProperty]
    private string _componentName = string.Empty;

    [ObservableProperty]
    private string _componentType = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    /// <summary>
    /// The underlying component instance.
    /// </summary>
    public IConfigurable Component { get; }

    /// <summary>
    /// Parameters exposed by this component, ordered by Group then Order.
    /// </summary>
    public ObservableCollection<ParameterViewModel> Parameters { get; } = [];

    /// <summary>
    /// UI elements declared via IDisplayable for dynamic frontend rendering.
    /// </summary>
    public ObservableCollection<UiElementViewModel> DisplayElements { get; } = [];

    /// <summary>
    /// Whether this component implements IDisplayable with any elements.
    /// </summary>
    public bool HasDisplayElements => DisplayElements.Count > 0;

    /// <summary>
    /// Category used for UI filtering: "Bridge" or "Sensor".
    /// </summary>
    public string Category => Component is ICanBridge ? "Bridge" : "Sensor";

    public ComponentViewModel(IConfigurable component)
    {
        Component = component;
        ComponentName = component is IComponent c ? c.ToString() ?? component.GetType().Name : component.GetType().Name;
        ComponentType = component.GetType().Name;

        _displayable = component as IDisplayable;

        RefreshParameters();
        RefreshDisplayElements();
        StartDisplayRefresh();
    }

    /// <summary>
    /// Rebuild the parameter list from the component.
    /// </summary>
    public void RefreshParameters()
    {
        Parameters.Clear();
        foreach (var param in Component.Parameters.OrderBy(p => string.Join("/", p.Groups)).ThenBy(p => p.Order))
        {
            Parameters.Add(new ParameterViewModel(Component, param));
        }
    }

    /// <summary>
    /// Rebuild the display elements list from IDisplayable.
    /// </summary>
    public void RefreshDisplayElements()
    {
        DisplayElements.Clear();
        if (_displayable == null) return;

        try
        {
            var elements = _displayable.GetDisplayElements();
            foreach (var el in elements.OrderBy(e => e.Order))
            {
                DisplayElements.Add(new UiElementViewModel(
                    el, Component as IConfigurable));
            }
        }
        catch
        {
            // Skip components that fail to provide display elements
        }
    }

    /// <summary>
    /// Start a timer that periodically refreshes Display element values.
    /// </summary>
    private void StartDisplayRefresh()
    {
        if (_displayable == null) return;

        var timer = new System.Threading.Timer(_ =>
        {
            foreach (var el in DisplayElements)
            {
                el.RefreshValue(Component);
                foreach (var child in el.Children)
                    child.RefreshValue(Component);
            }
        }, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

        _refreshTimer = timer;
    }

    /// <summary>
    /// Stop the display refresh timer.
    /// </summary>
    public void StopDisplayRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }
}
