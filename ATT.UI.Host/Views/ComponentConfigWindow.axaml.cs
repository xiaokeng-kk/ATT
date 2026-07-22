using Avalonia.Controls;
using ATT.UI.Host.ViewModels;

namespace ATT.UI.Host.Views;

public partial class ComponentConfigWindow : Window
{
    public ComponentConfigWindow()
    {
        InitializeComponent();
    }

    public ComponentConfigWindow(ComponentViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
