using System.Collections.ObjectModel;
using System.IO.Ports;
using ATT.Cli.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// ViewModel for a single device entry in the device setup UI.
/// Manages type selection, transport config, and device name.
/// Instantiation-required elements only — no IConfigurable parameters.
/// </summary>
public partial class DeviceSetupItemViewModel : ObservableObject
{
    private Type? _actualType;

    /// <summary>Available device types for the ComboBox</summary>
    public IReadOnlyList<Type> AvailableTypes { get; }

    /// <summary>Display names for the type selection ComboBox</summary>
    public IReadOnlyList<string> TypeNames { get; }

    [ObservableProperty]
    private int _selectedTypeIndex = -1;

    [ObservableProperty]
    private string _deviceName = "";

    [ObservableProperty]
    private int _transportTypeIndex; // 0 = SerialPort, 1 = Tcp

    [ObservableProperty]
    private string _portName = "";

    [ObservableProperty]
    private int _baudRate = 1500000;

    [ObservableProperty]
    private string _tcpHost = "127.0.0.1";

    [ObservableProperty]
    private int _tcpPort = 8080;

    /// <summary>Available COM ports on the current PC</summary>
    public ObservableCollection<string> AvailablePorts { get; } = [];

    /// <summary>Whether SerialPort transport is selected</summary>
    public bool IsSerialPort => TransportTypeIndex == 0;

    /// <summary>Whether TCP transport is selected</summary>
    public bool IsTcp => TransportTypeIndex == 1;

    public DeviceSetupItemViewModel(IReadOnlyList<Type> availableTypes)
    {
        AvailableTypes = availableTypes;
        TypeNames = availableTypes
            .Select(t => t.Name)
            .ToList()
            .AsReadOnly();

        RefreshPorts();
    }

    /// <summary>Refresh the list of available COM ports</summary>
    [RelayCommand]
    public void RefreshPorts()
    {
        var currentPort = PortName;
        AvailablePorts.Clear();
        foreach (var port in SerialPort.GetPortNames())
        {
            AvailablePorts.Add(port);
        }

        // Restore previous selection if still available, otherwise pick first
        PortName = AvailablePorts.Contains(currentPort) ? currentPort
                 : AvailablePorts.FirstOrDefault() ?? "";
    }

    partial void OnSelectedTypeIndexChanged(int value)
    {
        if (value < 0 || value >= AvailableTypes.Count) return;
        _actualType = AvailableTypes[value];
        DeviceName = _actualType?.Name ?? "";
    }

    partial void OnTransportTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSerialPort));
        OnPropertyChanged(nameof(IsTcp));
    }

    /// <summary>
    /// Convert this UI state to a DeviceConfig for JSON serialization to ATT.Cli.
    /// Only includes instantiation-required fields — no IConfigurable parameters.
    /// </summary>
    public DeviceConfig ToDeviceConfig()
    {
        return new DeviceConfig
        {
            Name = DeviceName,
            Type = _actualType?.Name ?? "",
            Transport = new TransportConfig
            {
                Type = IsSerialPort ? "SerialPort" : "Tcp",
                PortName = PortName,
                BaudRate = BaudRate,
                Host = IsTcp ? TcpHost : null,
                RemotePort = IsTcp ? TcpPort : null,
            }
        };
    }
}
