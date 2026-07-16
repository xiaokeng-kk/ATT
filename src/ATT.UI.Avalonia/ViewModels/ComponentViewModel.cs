using System.Collections.ObjectModel;
using ATT.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ATT.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel wrapping an IConfigurable component.
/// Exposes its parameters as an observable collection for dynamic UI binding.
/// </summary>
public partial class ComponentViewModel : ObservableObject
{
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

    public ComponentViewModel(IConfigurable component)
    {
        Component = component;
        ComponentName = component is IComponent c ? c.ToString() ?? component.GetType().Name : component.GetType().Name;
        ComponentType = component.GetType().Name;

        RefreshParameters();
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
}
