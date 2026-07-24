using System.Windows.Input;
using ATT.Cli.Models;
using ATT.Core.Models;
using ATT.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// ViewModel wrapping a single ConfigurationParameter or RuntimeParameter.
/// Provides observable properties and commands for the UI to bind to.
/// When wrapping RuntimeParameter (from CLI subprocess), commands are no-ops
/// since there is no live device reference. For in-process use,
/// wrap ConfigurationParameter + IConfigurable to enable real commands.
/// </summary>
public partial class ParameterViewModel : ObservableObject
{
    private readonly IConfigurable? _component;

    /// <summary>
    /// In-process constructor: wraps a live IConfigurable + ConfigurationParameter.
    /// Commands will invoke SetParameter/InvokeAction on the actual device.
    /// </summary>
    public ParameterViewModel(IConfigurable component, ConfigurationParameter parameter)
    {
        _component = component;
        Name = parameter.Name;
        Description = parameter.Description;
        ParameterType = parameter.ParameterType;
        Groups = parameter.Groups;
        EnumOptions = parameter.EnumOptions;
        MinValue = parameter.MinValue;
        MaxValue = parameter.MaxValue;
        Order = parameter.Order;

        _currentValue = parameter.CurrentValue ?? parameter.DefaultValue;
    }

    /// <summary>
    /// Subprocess constructor: wraps a RuntimeParameter (CLI JSON snapshot).
    /// Commands are no-ops since there is no live device reference.
    /// Use when UI receives device state via ATT.Cli subprocess output.
    /// </summary>
    public ParameterViewModel(RuntimeParameter parameter)
    {
        _component = null;
        Name = parameter.Name;
        Description = parameter.Description;
        Groups = []; // RuntimeParameter has no group info
        ParameterType = parameter.ParameterType switch
        {
            "Action" => ParameterType.Action,
            "Integer" => ParameterType.Integer,
            "Double" => ParameterType.Double,
            "Boolean" => ParameterType.Boolean,
            "String" => ParameterType.String,
            "Enum" => ParameterType.Enum,
            _ => ParameterType.String
        };
        MinValue = parameter.MinValue;
        MaxValue = parameter.MaxValue;
        _currentValue = parameter.CurrentValue;
    }

    // ==================== Read-only metadata ====================

    public string Name { get; }
    public string Description { get; }
    public ParameterType ParameterType { get; }
    public string[] Groups { get; }
    public IReadOnlyList<(string Name, object Value)>? EnumOptions { get; }
    public double? MinValue { get; }
    public double? MaxValue { get; }
    public double Order { get; }

    // ==================== Observable state ====================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentValueNumeric))]
    [NotifyPropertyChangedFor(nameof(CurrentValueBool))]
    [NotifyPropertyChangedFor(nameof(CurrentValueString))]
    private object? _currentValue;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    // ==================== Computed display helpers ====================

    /// <summary>Action types render as a Button only</summary>
    public bool IsAction => ParameterType == ParameterType.Action;

    /// <summary>Boolean → CheckBox</summary>
    public bool IsBoolean => ParameterType == ParameterType.Boolean;

    /// <summary>Integer / Double → NumericUpDown</summary>
    public bool IsNumeric => ParameterType is ParameterType.Integer or ParameterType.Double;

    /// <summary>String / Enum / fallback → TextBox</summary>
    public bool IsTextInput => !IsAction && !IsBoolean && !IsNumeric;

    /// <summary>Value types render as an input control + Set button</summary>
    public bool IsValueType => !IsAction;

    /// <summary>Numeric value for NumericUpDown binding</summary>
    public double CurrentValueNumeric
    {
        get => CurrentValue is IConvertible c ? Convert.ToDouble(c) : 0;
        set
        {
            if (ParameterType == ParameterType.Integer)
                CurrentValue = (long)value;
            else
                CurrentValue = value;
        }
    }

    /// <summary>Boolean value for CheckBox binding</summary>
    public bool CurrentValueBool
    {
        get => CurrentValue is bool b && b;
        set => CurrentValue = value;
    }

    public string CurrentValueString
    {
        get => CurrentValue?.ToString() ?? "";
        set => ParseAndSetValue(value);
    }

    // ==================== Commands ====================

    /// <summary>For Action type: invoked on button click</summary>
    public ICommand InvokeCommand => new RelayCommand(() =>
    {
        if (_component == null) return; // No-op in subprocess mode
        try
        {
            IsBusy = true;
            _component.InvokeAction(Name);
            StatusMessage = "OK";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    });

    /// <summary>For value types: apply the current input value</summary>
    public ICommand SetCommand => new RelayCommand(() =>
    {
        if (_component == null) return; // No-op in subprocess mode
        try
        {
            IsBusy = true;
            _component.SetParameter(Name, CurrentValue);
            StatusMessage = "OK";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    });

    // ==================== Helpers ====================

    public void SetParameterValue(object? value)
    {
        CurrentValue = value;
    }

    /// <summary>
    /// Parse a string input into the correct type based on ParameterType.
    /// Called by the View when the user types a value.
    /// </summary>
    public void ParseAndSetValue(string text)
    {
        try
        {
            object? parsed = ParameterType switch
            {
                ParameterType.Integer => long.Parse(text),
                ParameterType.Double => double.Parse(text),
                ParameterType.Boolean => bool.Parse(text),
                _ => text,
            };
            CurrentValue = parsed;
        }
        catch
        {
            StatusMessage = "Invalid input";
        }
    }
}
