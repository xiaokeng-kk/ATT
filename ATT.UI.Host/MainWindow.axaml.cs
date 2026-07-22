using ATT.UI.Host.ViewModels;
using ATT.UI.Host.Views;
using Avalonia.Controls;

namespace ATT.UI.Host;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var mainVm = new MainWindowViewModel();
        DataContext = mainVm;

        // Wire up auto-open config windows (legacy component manager)
        mainVm.ComponentManager.OpenConfigWindow = vm =>
        {
            var win = new ComponentConfigWindow(vm);
            if (VisualRoot is Window owner)
                win.Show(owner);
            else
                win.Show();
            return true;
        };
    }
}