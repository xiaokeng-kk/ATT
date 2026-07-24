using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ATT.UI.Host.ViewModels;

namespace ATT.UI.Host.Views;

/// <summary>
/// Selects the appropriate DataTemplate for a ParameterViewModel based on its ParameterType.
/// Maps:
///   Action       → Button (InvokeCommand)
///   Integer/Double → TextBox + Set Button (SetCommand)
///   Boolean      → ToggleSwitch (SetCommand)
///   String/Enum  → TextBox + Send Button (SetCommand)
/// </summary>
public class ParameterTemplateSelector : AvaloniaObject, IDataTemplate
{
    public static readonly DirectProperty<ParameterTemplateSelector, IDataTemplate?> ActionTemplateProperty =
        AvaloniaProperty.RegisterDirect<ParameterTemplateSelector, IDataTemplate?>(
            nameof(ActionTemplate), o => o.ActionTemplate, (o, v) => o.ActionTemplate = v);

    public static readonly DirectProperty<ParameterTemplateSelector, IDataTemplate?> NumericTemplateProperty =
        AvaloniaProperty.RegisterDirect<ParameterTemplateSelector, IDataTemplate?>(
            nameof(NumericTemplate), o => o.NumericTemplate, (o, v) => o.NumericTemplate = v);

    public static readonly DirectProperty<ParameterTemplateSelector, IDataTemplate?> BooleanTemplateProperty =
        AvaloniaProperty.RegisterDirect<ParameterTemplateSelector, IDataTemplate?>(
            nameof(BooleanTemplate), o => o.BooleanTemplate, (o, v) => o.BooleanTemplate = v);

    public static readonly DirectProperty<ParameterTemplateSelector, IDataTemplate?> TextTemplateProperty =
        AvaloniaProperty.RegisterDirect<ParameterTemplateSelector, IDataTemplate?>(
            nameof(TextTemplate), o => o.TextTemplate, (o, v) => o.TextTemplate = v);

    public IDataTemplate? ActionTemplate { get; set; }
    public IDataTemplate? NumericTemplate { get; set; }
    public IDataTemplate? BooleanTemplate { get; set; }
    public IDataTemplate? TextTemplate { get; set; }

    public Control? Build(object? param)
    {
        var template = SelectTemplate(param);
        return template?.Build(param);
    }

    public bool Match(object? data)
    {
        return data is ParameterViewModel;
    }

    private IDataTemplate? SelectTemplate(object? item)
    {
        if (item is ParameterViewModel vm)
        {
            if (vm.IsAction) return ActionTemplate;
            if (vm.IsNumeric) return NumericTemplate;
            if (vm.IsBoolean) return BooleanTemplate;
            // IsTextInput (String / Enum / fallback)
            return TextTemplate;
        }
        return TextTemplate;
    }
}
