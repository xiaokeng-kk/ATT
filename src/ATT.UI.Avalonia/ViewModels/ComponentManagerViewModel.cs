using System.Collections.ObjectModel;
using ATT.Core;
using ATT.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ATT.UI.Avalonia.ViewModels;

/// <summary>
/// Top-level ViewModel that manages all discovered components.
/// Host apps bind this to their main window DataContext.
/// </summary>
public partial class ComponentManagerViewModel : ObservableObject
{
    private readonly ComponentCatalog _catalog;

    [ObservableProperty]
    private ComponentViewModel? _selectedComponent;

    /// <summary>
    /// All discovered component ViewModels.
    /// </summary>
    public ObservableCollection<ComponentViewModel> Components { get; } = [];

    public ComponentManagerViewModel(ComponentCatalog catalog)
    {
        _catalog = catalog;
        _catalog.CatalogChanged += OnCatalogChanged;
    }

    private void OnCatalogChanged()
    {
        RefreshComponents();
    }

    /// <summary>
    /// Scan all IConfigurable components in the catalog and create ViewModels.
    /// </summary>
    [RelayCommand]
    public void RefreshComponents()
    {
        var configurableTypes = _catalog.GetComponents<IConfigurable>();
        var existingNames = Components.Select(vm => vm.ComponentType).ToHashSet();
        var newTypes = configurableTypes.Where(t => !existingNames.Contains(t.Name)).ToList();

        foreach (var type in newTypes)
        {
            try
            {
                // The host app must register transport instances before creating sensors
                var instance = _catalog.CreateInstance<IConfigurable>(type);
                if (instance != null)
                    Components.Add(new ComponentViewModel(instance));
            }
            catch
            {
                // Skip components that cannot be instantiated (e.g. missing constructor args)
            }
        }
    }

    /// <summary>
    /// Manually add a pre-configured component.
    /// </summary>
    public void AddComponent(IConfigurable component)
    {
        var vm = new ComponentViewModel(component);
        Components.Add(vm);
        SelectedComponent = vm;
    }

    /// <summary>
    /// Remove a component ViewModel.
    /// </summary>
    [RelayCommand]
    public void RemoveComponent(ComponentViewModel vm)
    {
        Components.Remove(vm);
        if (SelectedComponent == vm)
            SelectedComponent = Components.FirstOrDefault();
    }
}
