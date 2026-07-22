using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ATT.Core.Models;
using ATT.UI.Host.ViewModels;

namespace ATT.UI.Host.Views;

/// <summary>
/// Selects the appropriate DataTemplate for a UiElementViewModel based on its Type.
/// Implements IDataTemplate to serve as a composite template selector
/// that delegates to type-specific templates defined in XAML resources.
/// </summary>
public class UiElementTemplateSelector : Avalonia.AvaloniaObject, IDataTemplate
{
    public static readonly DirectProperty<UiElementTemplateSelector, IDataTemplate?> ButtonTemplateProperty =
        AvaloniaProperty.RegisterDirect<UiElementTemplateSelector, IDataTemplate?>(
            nameof(ButtonTemplate), o => o.ButtonTemplate, (o, v) => o.ButtonTemplate = v);

    public static readonly DirectProperty<UiElementTemplateSelector, IDataTemplate?> InputButtonTemplateProperty =
        AvaloniaProperty.RegisterDirect<UiElementTemplateSelector, IDataTemplate?>(
            nameof(InputButtonTemplate), o => o.InputButtonTemplate, (o, v) => o.InputButtonTemplate = v);

    public static readonly DirectProperty<UiElementTemplateSelector, IDataTemplate?> ToggleTemplateProperty =
        AvaloniaProperty.RegisterDirect<UiElementTemplateSelector, IDataTemplate?>(
            nameof(ToggleTemplate), o => o.ToggleTemplate, (o, v) => o.ToggleTemplate = v);

    public static readonly DirectProperty<UiElementTemplateSelector, IDataTemplate?> DisplayTemplateProperty =
        AvaloniaProperty.RegisterDirect<UiElementTemplateSelector, IDataTemplate?>(
            nameof(DisplayTemplate), o => o.DisplayTemplate, (o, v) => o.DisplayTemplate = v);

    public static readonly DirectProperty<UiElementTemplateSelector, IDataTemplate?> ChartTemplateProperty =
        AvaloniaProperty.RegisterDirect<UiElementTemplateSelector, IDataTemplate?>(
            nameof(ChartTemplate), o => o.ChartTemplate, (o, v) => o.ChartTemplate = v);

    public static readonly DirectProperty<UiElementTemplateSelector, IDataTemplate?> GroupTemplateProperty =
        AvaloniaProperty.RegisterDirect<UiElementTemplateSelector, IDataTemplate?>(
            nameof(GroupTemplate), o => o.GroupTemplate, (o, v) => o.GroupTemplate = v);

    public IDataTemplate? ButtonTemplate { get; set; }
    public IDataTemplate? InputButtonTemplate { get; set; }
    public IDataTemplate? ToggleTemplate { get; set; }
    public IDataTemplate? DisplayTemplate { get; set; }
    public IDataTemplate? ChartTemplate { get; set; }
    public IDataTemplate? GroupTemplate { get; set; }

    public Control? Build(object? param)
    {
        var template = SelectTemplate(param);
        return template?.Build(param);
    }

    public bool Match(object? data)
    {
        return data is UiElementViewModel;
    }

    private IDataTemplate? SelectTemplate(object? item)
    {
        if (item is UiElementViewModel vm)
        {
            return vm.Type switch
            {
                UiElementType.Button => ButtonTemplate,
                UiElementType.InputButton => InputButtonTemplate,
                UiElementType.Toggle => ToggleTemplate,
                UiElementType.Display => DisplayTemplate,
                UiElementType.Chart => ChartTemplate,
                UiElementType.Group => GroupTemplate,
                _ => ButtonTemplate,
            };
        }
        return ButtonTemplate;
    }
}
