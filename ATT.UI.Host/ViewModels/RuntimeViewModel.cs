using System.Collections.ObjectModel;
using System.Text.Json;
using ATT.Cli.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// ViewModel for the runtime monitoring view.
/// Manages runtime device states and provides return-to-setup navigation.
/// </summary>
public partial class RuntimeViewModel : ObservableObject
{
    /// <summary>Runtime device states</summary>
    public ObservableCollection<RuntimeDeviceViewModel> Devices { get; } = [];

    /// <summary>Whether any devices are being monitored</summary>
    public bool HasDevices => Devices.Count > 0;

    /// <summary>Overall connection status</summary>
    public string OverallStatus => Devices.Count > 0 && Devices.All(d => d.Connected)
        ? "All devices connected"
        : "Some devices disconnected";

    /// <summary>
    /// Event raised when the user wants to return to setup mode.
    /// </summary>
    public event Action? ReturnToSetupRequested;

    /// <summary>
    /// Parse runtime JSON from CLI stdout and populate the device list.
    /// </summary>
    public void LoadFromJson(string runtimeJson)
    {
        Devices.Clear();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var output = JsonSerializer.Deserialize<RuntimeOutput>(runtimeJson, options);
            if (output?.Devices == null) return;

            foreach (var rd in output.Devices)
            {
                var vm = new RuntimeDeviceViewModel
                {
                    Name = rd.Name,
                    Type = rd.Type,
                    Connected = rd.Connected,
                    Unit = rd.Unit,
                    ReadValue = rd.ReadValue,
                };
                vm.ParseDisplayJson(rd.DisplayJson);
                vm.ParseParameters(rd.Parameters);
                Devices.Add(vm);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Runtime JSON parse error: {ex.Message}");
        }

        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(OverallStatus));
    }

    /// <summary>
    /// Command to return to device setup mode.
    /// </summary>
    [RelayCommand]
    public void ReturnToSetup()
    {
        ReturnToSetupRequested?.Invoke();
    }
}
