using ATT.Core;
using ATT.Core.Models;
using ATT.Protocol.Sensors;
using ATT.Protocol.Transports;
using ATT.UI.Host.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// Main window ViewModel.
/// Sets up the component catalog and adds a sample 500A current sensor.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    public ComponentManagerViewModel ComponentManager { get; }

    [ObservableProperty]
    private string _title = "ATT - Automated Test Toolkit";

    public MainWindowViewModel()
    {
        // Set up component catalog and register known types
        var catalog = new ComponentCatalog();
        catalog.Register<CurrentSensor500A>();

        ComponentManager = new ComponentManagerViewModel(catalog);

        // Create a sample 500A sensor with a serial transport
        // (transport is not opened/connected — just demonstrates UI binding)
        var transport = new SerialPortTransport(new UartConfig
        {
            PortName = "COM1",
            BaudRate = 115200
        });
        var sensor = new CurrentSensor500A(transport)
        {
            Name = "Arc Fault 500A Sensor"
        };
        ComponentManager.AddComponent(sensor);
    }
}
