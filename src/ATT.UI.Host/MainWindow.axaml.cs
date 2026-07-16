using ATT.UI.Host.ViewModels;
using Avalonia.Controls;

namespace ATT.UI.Host;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}