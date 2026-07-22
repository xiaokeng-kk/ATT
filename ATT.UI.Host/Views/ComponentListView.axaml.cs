using Avalonia.Controls;
using Avalonia.Input;
using ATT.UI.Host.ViewModels;

namespace ATT.UI.Host.Views;

public partial class ComponentListView : UserControl
{
    public ComponentListView()
    {
        InitializeComponent();
    }

    private void OnComponentDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ComponentManagerViewModel manager) return;
        if (manager.SelectedComponent == null) return;

        // Re-open the config window on double-tap
        manager.OpenConfigWindow?.Invoke(manager.SelectedComponent);
    }
}
