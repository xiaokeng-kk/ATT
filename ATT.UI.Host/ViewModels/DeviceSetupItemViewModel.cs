using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
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

    /// <summary>Available COM ports on the current PC, with device descriptions</summary>
    public ObservableCollection<ComPortInfo> AvailablePorts { get; } = [];

    [ObservableProperty]
    private ComPortInfo? _selectedPortInfo;

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

    partial void OnSelectedPortInfoChanged(ComPortInfo? value)
    {
        PortName = value?.PortName ?? "";
    }

    /// <summary>Refresh the list of available COM ports with device descriptions</summary>
    [RelayCommand]
    public void RefreshPorts()
    {
        var currentPort = PortName;

        // Query WMI for device descriptions keyed by port name
        var descriptions = GetPortDescriptions();

        AvailablePorts.Clear();
        foreach (var port in SerialPort.GetPortNames())
        {
            var desc = descriptions.TryGetValue(port, out var d) ? d : port;
            AvailablePorts.Add(new ComPortInfo { PortName = port, Description = desc });
        }

        // Restore previous selection if still available, otherwise pick first
        SelectedPortInfo = AvailablePorts.FirstOrDefault(p => p.PortName == currentPort)
                        ?? AvailablePorts.FirstOrDefault();
    }

    /// <summary>
    /// Query WMI Win32_PnPEntity to get friendly device descriptions for COM ports.
    /// Uses ManagementObjectSearcher which requires System.Management reference.
    /// Falls back to port name on failure.
    /// </summary>
    private static Dictionary<string, string> GetPortDescriptions()
    {
        var result = new Dictionary<string, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0 AND Name LIKE '%(COM%'");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (string.IsNullOrEmpty(name)) continue;

                // Parse "Description (COMx)" → description + portName
                var portMatch = Regex.Match(name, @"(COM\d+)");
                if (!portMatch.Success) continue;

                var portName = portMatch.Groups[1].Value;
                var desc = Regex.Replace(name, @"\s*\(COM\d+\)", "").Trim();
                result[portName] = desc;
            }
        }
        catch
        {
            // WMI may be unavailable (e.g. Linux/macOS) — fall back gracefully
        }
        return result;
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
