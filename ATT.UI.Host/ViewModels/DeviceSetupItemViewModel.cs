using System.Collections.ObjectModel;
using ATT.Cli.Models;
using ATT.Core.Base;
using ATT.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// ViewModel for a single device entry in the device setup UI.
/// Manages type selection, transport config, name, and IConfigurable parameters.
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
    private string _portName = "COM1";

    [ObservableProperty]
    private int _baudRate = 1500000;

    [ObservableProperty]
    private string _tcpHost = "127.0.0.1";

    [ObservableProperty]
    private int _tcpPort = 8080;

    /// <summary>
    /// IConfigurable parameters — exposed as ParameterViewModels
    /// so the UI can reuse the existing parameter DataTemplates.
    /// </summary>
    public ObservableCollection<ParameterViewModel> Parameters { get; } = [];

    /// <summary>
    /// Whether a device type has been selected and its parameters loaded.
    /// </summary>
    public bool HasParameters => Parameters.Count > 0;

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
    }

    partial void OnSelectedTypeIndexChanged(int value)
    {
        Parameters.Clear();
        if (value < 0 || value >= AvailableTypes.Count) return;

        _actualType = AvailableTypes[value];
        DeviceName = _actualType?.Name ?? "";

        // Try to instantiate the type to extract IConfigurable parameters
        if (_actualType == null) return;
        var instance = CreateInstance(_actualType);
        if (instance is IConfigurable configurable)
        {
            foreach (var param in configurable.Parameters)
            {
                Parameters.Add(new ParameterViewModel(configurable, param));
            }
            OnPropertyChanged(nameof(HasParameters));
        }
    }

    partial void OnTransportTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSerialPort));
        OnPropertyChanged(nameof(IsTcp));
    }

    /// <summary>
    /// Attempt to create an instance of the given type for parameter discovery.
    /// Uses a best-effort approach — may fail for types with complex constructors.
    /// </summary>
    private static object? CreateInstance(Type type)
    {
        try
        {
            // Try to find a constructor with ITransport parameter
            var ctor = type.GetConstructors()
                .FirstOrDefault(c =>
                {
                    var ps = c.GetParameters();
                    return ps.Length == 1 && typeof(ITransport).IsAssignableFrom(ps[0].ParameterType);
                });

            if (ctor != null)
            {
                // Create a dummy transport for construction
                var mockTransport = new MockTransport();
                return ctor.Invoke([mockTransport]);
            }

            // Fallback to parameterless constructor
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Convert this UI state to a DeviceConfig for JSON serialization to ATT.Cli
    /// </summary>
    public DeviceConfig ToDeviceConfig()
    {
        var config = new DeviceConfig
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
            },
            Parameters = new Dictionary<string, object?>()
        };

        // Collect parameter values
        foreach (var param in Parameters)
        {
            config.Parameters[param.Name] = param.CurrentValue;
        }

        return config;
    }

    /// <summary>
    /// Mock transport for parameter discovery — doesn't actually connect.
    /// </summary>
    private class MockTransport : ITransport
    {
        public string Name { get; set; } = "Mock";
        public bool IsConnected => false;
#pragma warning disable CS0067 // Unused events — required by ITransport interface
        public event Action<byte[]>? DataReceived;
        public event Action<bool>? ConnectionStateChanged;
#pragma warning restore CS0067

        public bool Open() => false;
        public bool Close() => true;
        public void SendRaw(byte[] data) { }
    }
}
