using ATT.Core;
using ATT.Protocol.Sensors;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// Main window ViewModel.
/// Manages mode switching between device setup and runtime monitoring.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "ATT - Automated Test Toolkit";

    // ==================== Modes ====================

    [ObservableProperty]
    private bool _isSetupMode = true;

    [ObservableProperty]
    private bool _isRuntimeMode;

    // ==================== ViewModels ====================

    /// <summary>
    /// Device setup ViewModel (mode 1: configure devices)
    /// </summary>
    public DeviceSetupViewModel DeviceSetup { get; }

    /// <summary>
    /// Runtime monitoring ViewModel (mode 2: monitor connected devices)
    /// </summary>
    public RuntimeViewModel Runtime { get; }

    /// <summary>
    /// Legacy component manager ViewModel (used for existing config windows)
    /// </summary>
    public ComponentManagerViewModel ComponentManager { get; }

    public MainWindowViewModel()
    {
        // Set up component catalog and register known types
        var catalog = new ComponentCatalog();
        catalog.Register<CurrentSensor500A>();

        ComponentManager = new ComponentManagerViewModel(catalog);
        DeviceSetup = new DeviceSetupViewModel(catalog);
        Runtime = new RuntimeViewModel();

        // Wire up: when CLI returns runtime data, switch to runtime mode
        DeviceSetup.OnRuntimeDataReceived += OnRuntimeDataReceived;

        // Wire up: when user clicks return to setup
        Runtime.ReturnToSetupRequested += OnReturnToSetupRequested;
    }

    // ==================== Mode Switching ====================

    /// <summary>
    /// Called when runtime JSON data is received from the CLI subprocess.
    /// Switches from setup mode to runtime monitoring mode.
    /// </summary>
    private void OnRuntimeDataReceived(string runtimeJson)
    {
        Runtime.LoadFromJson(runtimeJson);
        IsSetupMode = false;
        IsRuntimeMode = true;
    }

    /// <summary>
    /// Called when the user wants to return to device setup mode.
    /// </summary>
    private void OnReturnToSetupRequested()
    {
        IsRuntimeMode = false;
        IsSetupMode = true;
    }
}
