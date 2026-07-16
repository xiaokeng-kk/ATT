using Avalonia.Controls;
using ATT.UI.Host.ViewModels;

namespace ATT.UI.Host.Views;

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
