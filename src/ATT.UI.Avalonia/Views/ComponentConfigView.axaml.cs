using Avalonia.Controls;
using ATT.UI.Avalonia.ViewModels;

namespace ATT.UI.Avalonia.Views;

/// <summary>
/// Dynamic component configuration panel.
/// Binds to ComponentViewModel and renders controls for each parameter.
/// </summary>
public partial class ComponentConfigView : UserControl
{
    public ComponentConfigView()
    {
        InitializeComponent();
    }
}
