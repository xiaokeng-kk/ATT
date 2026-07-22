using System.Collections.ObjectModel;
using System.Windows.Input;
using ATT.Core.Interfaces;
using ATT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// ViewModel wrapping a single UiElement from the IDisplayable interface.
/// Provides observable properties and commands for the UI to bind to.
/// </summary>
public partial class UiElementViewModel : ObservableObject
{
    private readonly IConfigurable? _configurable;

    public UiElementViewModel(UiElement element, IConfigurable? configurable)
    {
        Element = element;
        _configurable = configurable;
        Id = element.Id;
        Label = element.Label;
        Description = element.Description ?? string.Empty;
        Type = element.Type;
        Unit = element.Unit ?? string.Empty;
        Order = element.Order;

        if (element.Children is { Count: > 0 })
        {
            foreach (var child in element.Children.OrderBy(c => c.Order))
                _children.Add(new UiElementViewModel(child, configurable));
        }
    }

    // ==================== Read-only metadata ====================

    public string Id { get; }
    public string Label { get; }
    public string Description { get; }
    public UiElementType Type { get; }
    public string Unit { get; }
    public double Order { get; }
    public UiElement Element { get; }

    public bool HasChildren => _children.Count > 0;

    // ==================== Observable state ====================

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    private readonly ObservableCollection<UiElementViewModel> _children = [];

    public IReadOnlyList<UiElementViewModel> Children => _children;

    // ==================== Commands ====================

    /// <summary>Button click → call IConfigurable.InvokeAction(Element.Action)</summary>
    public ICommand ActionCommand => new RelayCommand(() =>
    {
        if (_configurable == null || string.IsNullOrEmpty(Element.Action)) return;
        try
        {
            IsBusy = true;
            _configurable.InvokeAction(Element.Action);
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

    /// <summary>InputButton send → call action with input text as parameter</summary>
    public ICommand SendCommand => new RelayCommand(() =>
    {
        if (_configurable == null || string.IsNullOrEmpty(Element.Action)) return;
        try
        {
            IsBusy = true;
            _configurable.SetParameter(Element.Action, InputText);
            _configurable.InvokeAction(Element.Action);
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

    /// <summary>Toggle changed → call action or actionOff</summary>
    public ICommand ToggleCommand => new RelayCommand(() =>
    {
        if (_configurable == null) return;
        try
        {
            IsBusy = true;
            var action = IsChecked ? Element.Action : Element.ActionOff;
            if (!string.IsNullOrEmpty(action))
                _configurable.InvokeAction(action);
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

    /// <summary>Refresh the Value from the underlying component's property</summary>
    public void RefreshValue(object component)
    {
        if (Type != UiElementType.Display || string.IsNullOrEmpty(Element.Bind))
            return;

        try
        {
            var compType = component.GetType();
            var prop = compType.GetProperty(Element.Bind);
            if (prop != null)
            {
                var val = prop.GetValue(component);
                Value = val?.ToString() ?? "";
                if (val is double d)
                    Value = d.ToString("F3");
                else if (val is bool b)
                    Value = b ? "Yes" : "No";
            }
            else
            {
                var method = compType.GetMethod(Element.Bind, System.Type.EmptyTypes);
                if (method != null)
                {
                    var val = method.Invoke(component, null);
                    Value = val?.ToString() ?? "";
                    if (val is double d)
                        Value = d.ToString("F3");
                    else if (val is bool b)
                        Value = b ? "Yes" : "No";
                }
            }
        }
        catch
        {
            // Silently skip refresh errors
        }
    }
}
