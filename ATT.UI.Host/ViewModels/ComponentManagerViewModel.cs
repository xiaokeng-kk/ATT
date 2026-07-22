using System.Collections.ObjectModel;
using System.Windows.Input;
using ATT.Core;
using ATT.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// A node in the category menu tree.
/// </summary>
public class CategoryNode
{
    public string Name { get; init; } = "";
    public string? Category { get; init; }
    public string? ComponentType { get; init; }
    public ObservableCollection<CategoryNode> Children { get; init; } = [];
    public ICommand? SelectCommand { get; init; }
}

/// <summary>
/// Top-level ViewModel that manages all discovered components.
/// Host apps bind this to their main window DataContext.
/// </summary>
public partial class ComponentManagerViewModel : ObservableObject
{
    private readonly ComponentCatalog _catalog;

    [ObservableProperty]
    private ComponentViewModel? _selectedComponent;

    [ObservableProperty]
    private string _selectedCategory = "All";

    /// <summary>
    /// Callback invoked when a config window should be opened for a component.
    /// View layer sets this to create the actual Window.
    /// </summary>
    public Func<ComponentViewModel, bool>? OpenConfigWindow { get; set; }

    /// <summary>
    /// All discovered component ViewModels.
    /// </summary>
    public ObservableCollection<ComponentViewModel> Components { get; } = [];

    /// <summary>
    /// Components filtered by the selected category.
    /// </summary>
    public ObservableCollection<ComponentViewModel> FilteredComponents { get; } = [];

    /// <summary>
    /// Available categories for filtering.
    /// </summary>
    public string[] Categories { get; } = ["All", "Bridge", "Sensor"];

    /// <summary>
    /// Hierarchical menu items for multi-level dropdown.
    /// </summary>
    public ObservableCollection<CategoryNode> CategoryMenuItems { get; } = [];

    public ComponentManagerViewModel(ComponentCatalog catalog)
    {
        _catalog = catalog;
        _catalog.CatalogChanged += OnCatalogChanged;
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredComponents.Clear();
        foreach (var vm in Components)
        {
            if (SelectedCategory == "All" || vm.Category == SelectedCategory)
                FilteredComponents.Add(vm);
        }

        if (SelectedComponent != null && !FilteredComponents.Contains(SelectedComponent))
            SelectedComponent = FilteredComponents.FirstOrDefault();
    }

    private void RebuildCategoryMenu()
    {
        CategoryMenuItems.Clear();

        // Group components by category
        var groups = Components.GroupBy(vm => vm.Category).OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var categoryNode = new CategoryNode
            {
                Name = group.Key,
                Category = group.Key,
                SelectCommand = new RelayCommand(() => SelectedCategory = group.Key)
            };

            foreach (var vm in group.OrderBy(v => v.ComponentName))
            {
                var compName = vm.ComponentName;
                categoryNode.Children.Add(new CategoryNode
                {
                    Name = compName,
                    Category = group.Key,
                    ComponentType = vm.ComponentType,
                    SelectCommand = new RelayCommand(() =>
                    {
                        SelectedCategory = group.Key;
                        SelectedComponent = vm;
                    })
                });
            }

            CategoryMenuItems.Add(categoryNode);
        }
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
    /// Manually add a pre-configured component and auto-open its config window.
    /// </summary>
    public void AddComponent(IConfigurable component)
    {
        var vm = new ComponentViewModel(component);
        Components.Add(vm);
        RebuildCategoryMenu();
        ApplyFilter();
        SelectedComponent = vm;

        // Auto-open config window (View layer handles the actual Window creation)
        OpenConfigWindow?.Invoke(vm);
    }

    /// <summary>
    /// Remove a component ViewModel.
    /// </summary>
    [RelayCommand]
    public void RemoveComponent(ComponentViewModel vm)
    {
        Components.Remove(vm);
        FilteredComponents.Remove(vm);
        RebuildCategoryMenu();
        if (SelectedComponent == vm)
            SelectedComponent = FilteredComponents.FirstOrDefault();
    }
}
