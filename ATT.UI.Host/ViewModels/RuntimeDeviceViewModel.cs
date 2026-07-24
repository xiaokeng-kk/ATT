using System.Collections.ObjectModel;
using System.Text.Json;
using ATT.Cli.Models;
using ATT.Core.Interfaces;
using ATT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// ViewModel wrapping a single device's runtime state from CLI stdout.
/// Parses displayJson into UiElementViewModels for the runtime view.
/// Converts RuntimeParameter objects into ParameterViewModels with
/// interactive command bindings for the runtime UI.
/// </summary>
public partial class RuntimeDeviceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _type = "";

    [ObservableProperty]
    private bool _connected;

    [ObservableProperty]
    private string _unit = "";

    [ObservableProperty]
    private double _readValue;

    /// <summary>Formatted read value string for display</summary>
    public string ReadValueDisplay => $"{ReadValue:F3} {Unit}".Trim();

    /// <summary>Status color based on connection state</summary>
    public string StatusColor => Connected ? "#4CAF50" : "#F44336";

    /// <summary>Connection status text</summary>
    public string StatusText => Connected ? "Connected" : "Disconnected";

    /// <summary>UI elements parsed from displayJson</summary>
    public ObservableCollection<UiElementViewModel> DisplayElements { get; } = [];

    /// <summary>Whether this device has IDisplayable controls</summary>
    public bool HasDisplayElements => DisplayElements.Count > 0;

    /// <summary>
    /// Interactive parameter controls derived from IConfigurable.Parameters.
    /// Each ParameterViewModel provides commands (InvokeCommand, SetCommand)
    /// that the runtime UI binds to Button / TextBox / Toggle controls.
    /// </summary>
    public ObservableCollection<ParameterViewModel> Parameters { get; } = [];

    /// <summary>Whether this device has configurable parameters</summary>
    public bool HasParameters => Parameters.Count > 0;

    public RuntimeDeviceViewModel()
    {
    }

    /// <summary>
    /// Parse the displayJson string into UiElementViewModels.
    /// </summary>
    public void ParseDisplayJson(string? displayJson)
    {
        DisplayElements.Clear();
        if (string.IsNullOrWhiteSpace(displayJson)) return;

        try
        {
            var doc = JsonDocument.Parse(displayJson);
            if (!doc.RootElement.TryGetProperty("elements", out var elementsProp))
                return;

            var elements = JsonSerializer.Deserialize<List<UiElement>>(
                elementsProp.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (elements == null) return;

            foreach (var el in elements.OrderBy(e => e.Order))
            {
                DisplayElements.Add(new UiElementViewModel(el, null));
            }
        }
        catch
        {
            // Ignore parse errors
        }

        OnPropertyChanged(nameof(HasDisplayElements));
    }

    /// <summary>
    /// Populate Parameters collection from runtime parameter list.
    /// Converts RuntimeParameter data objects into ParameterViewModels
    /// with interactive command bindings.
    /// </summary>
    public void ParseParameters(List<RuntimeParameter>? parameters)
    {
        Parameters.Clear();
        if (parameters != null)
        {
            foreach (var p in parameters)
                Parameters.Add(new ParameterViewModel(p));
        }
        OnPropertyChanged(nameof(HasParameters));
    }

    partial void OnReadValueChanged(double value)
    {
        OnPropertyChanged(nameof(ReadValueDisplay));
    }

    partial void OnUnitChanged(string value)
    {
        OnPropertyChanged(nameof(ReadValueDisplay));
    }

    partial void OnConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusText));
    }
}
