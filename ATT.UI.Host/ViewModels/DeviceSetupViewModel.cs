using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using ATT.Cli.Models;
using ATT.Core;
using ATT.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ATT.UI.Host.ViewModels;

/// <summary>
/// ViewModel for the device setup view.
/// Manages device list, configuration, JSON generation, and CLI subprocess launch.
/// </summary>
public partial class DeviceSetupViewModel : ObservableObject
{
    private readonly ComponentCatalog _catalog;

    /// <summary>Available device types from the catalog (IConfigurable implementations)</summary>
    public IReadOnlyList<Type> AvailableTypes { get; }

    /// <summary>Device items being configured</summary>
    public ObservableCollection<DeviceSetupItemViewModel> DeviceItems { get; } = [];

    [ObservableProperty]
    private DeviceSetupItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _jsonPreview = "";

    [ObservableProperty]
    private bool _isLaunching;

    [ObservableProperty]
    private string? _launchStatus;

    /// <summary>Whether the device list has any items</summary>
    public bool HasDevices => DeviceItems.Count > 0;

    /// <summary>Whether a device item is currently selected in the list</summary>
    public bool HasSelectedItem => SelectedItem != null;

    public DeviceSetupViewModel(ComponentCatalog catalog)
    {
        _catalog = catalog;
        AvailableTypes = catalog.GetComponents<IConfigurable>().ToList().AsReadOnly();
    }

    partial void OnSelectedItemChanged(DeviceSetupItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        RefreshJsonPreview();
    }

    /// <summary>
    /// Add a new device entry to the setup list.
    /// </summary>
    [RelayCommand]
    public void AddDevice()
    {
        var item = new DeviceSetupItemViewModel(AvailableTypes);
        DeviceItems.Add(item);
        SelectedItem = item;
        OnPropertyChanged(nameof(HasDevices));
    }

    /// <summary>
    /// Remove a device entry from the setup list.
    /// If no item is specified, removes the most recently added device.
    /// </summary>
    [RelayCommand]
    public void RemoveDevice(DeviceSetupItemViewModel? item)
    {
        item ??= DeviceItems.LastOrDefault();
        if (item == null) return;
        DeviceItems.Remove(item);
        if (SelectedItem == item)
            SelectedItem = DeviceItems.FirstOrDefault();
        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(HasSelectedItem));
        RefreshJsonPreview();
    }

    /// <summary>
    /// Load device configurations from a JSON config file and populate the setup list.
    /// Looks up device types by name in AvailableTypes.
    /// </summary>
    public void LoadFromConfigFile(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            var configFile = JsonSerializer.Deserialize<DeviceConfigFile>(json);
            if (configFile?.Devices == null || configFile.Devices.Count == 0) return;

            foreach (var deviceCfg in configFile.Devices)
            {
                // Find matching type by name
                var typeIndex = -1;
                for (int i = 0; i < AvailableTypes.Count; i++)
                {
                    if (AvailableTypes[i].Name == deviceCfg.Type)
                    {
                        typeIndex = i;
                        break;
                    }
                }
                if (typeIndex < 0) continue;

                var item = new DeviceSetupItemViewModel(AvailableTypes);
                item.SelectedTypeIndex = typeIndex;
                item.DeviceName = deviceCfg.Name;

                if (deviceCfg.Transport != null)
                {
                    var t = deviceCfg.Transport;
                    item.TransportTypeIndex = t.Type == "Tcp" ? 1 : 0;
                    if (!string.IsNullOrEmpty(t.PortName))
                    {
                        // Try to match a COM port, otherwise set PortName directly
                        var matched = item.AvailablePorts
                            .FirstOrDefault(p => p.PortName == t.PortName);
                        if (matched != null)
                            item.SelectedPortInfo = matched;
                        else
                            item.PortName = t.PortName;
                    }
                    item.BaudRate = t.BaudRate > 0 ? t.BaudRate : 1500000;
                    item.TcpHost = t.Host ?? "127.0.0.1";
                    item.TcpPort = t.RemotePort ?? 8080;
                }

                DeviceItems.Add(item);
            }

            SelectedItem = DeviceItems.FirstOrDefault();
            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(HasSelectedItem));
            RefreshJsonPreview();
        }
        catch (Exception ex)
        {
            LaunchStatus = $"加载配置失败: {ex.Message}";
        }
    }

    /// <summary>
    /// Generate JSON string from the current device configurations.
    /// </summary>
    [RelayCommand]
    public void GenerateJson()
    {
        RefreshJsonPreview();
    }

    private void RefreshJsonPreview()
    {
        var configs = DeviceItems
            .Where(d => !string.IsNullOrEmpty(d.DeviceName))
            .Select(d => d.ToDeviceConfig())
            .ToList();

        var wrapper = new { devices = configs };
        JsonPreview = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Export the current configuration to a JSON file.
    /// </summary>
    [RelayCommand]
    public async Task ExportConfig()
    {
        RefreshJsonPreview();
        if (string.IsNullOrWhiteSpace(JsonPreview)) return;

        // Use a simple file path — in real app this would use a SaveFileDialog
        var filePath = Path.Combine(
            Environment.CurrentDirectory,
            $"att-config-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        try
        {
            await File.WriteAllTextAsync(filePath, JsonPreview);
            LaunchStatus = $"配置已导出: {filePath}";
        }
        catch (Exception ex)
        {
            LaunchStatus = $"导出失败: {ex.Message}";
        }
    }

    /// <summary>
    /// Launch ATT.Cli as a subprocess with the current configuration.
    /// Captures stdout to retrieve runtime JSON.
    /// </summary>
    [RelayCommand]
    public async Task LaunchCli()
    {
        RefreshJsonPreview();
        if (string.IsNullOrWhiteSpace(JsonPreview)) return;

        IsLaunching = true;
        LaunchStatus = "正在启动 ATT.Cli...";

        try
        {
            // Write config to a temp file
            var configPath = Path.Combine(Path.GetTempPath(), $"att-config-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(configPath, JsonPreview);

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{GetCliProjectPath()}\" -- --config \"{configPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait up to 30 seconds
            var exited = process.WaitForExit(30_000);
            if (!exited)
            {
                process.Kill();
                LaunchStatus = "ATT.Cli 启动超时";
                IsLaunching = false;
                return;
            }

            // Parse runtime JSON from stdout
            var output = outputBuilder.ToString();
            var errors = errorBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(errors))
            {
                LaunchStatus = $"ATT.Cli 输出错误: {errors.Trim()}";
                IsLaunching = false;
                return;
            }

            // Extract JSON between markers
            var json = ExtractRuntimeJson(output);
            if (json != null)
            {
                LaunchStatus = "设备启动成功，切换到运行模式";
                // Signal the parent ViewModel to switch to runtime mode
                OnRuntimeDataReceived?.Invoke(json);
            }
            else
            {
                LaunchStatus = "未检测到设备运行时数据";
            }

            // Clean up temp config file
            try { File.Delete(configPath); } catch { }
        }
        catch (Exception ex)
        {
            LaunchStatus = $"启动失败: {ex.Message}";
        }
        finally
        {
            IsLaunching = false;
        }
    }

    /// <summary>
    /// Event raised when runtime JSON data is received from the CLI subprocess.
    /// Parent ViewModel subscribes to this to switch to runtime mode.
    /// </summary>
    public event Action<string>? OnRuntimeDataReceived;

    /// <summary>
    /// Extract runtime JSON from CLI output between ===RUNTIME_JSON=== markers.
    /// </summary>
    private static string? ExtractRuntimeJson(string output)
    {
        const string startMarker = "===RUNTIME_JSON===";
        const string endMarker = "===RUNTIME_JSON_END===";

        var startIdx = output.IndexOf(startMarker);
        if (startIdx < 0) return null;

        startIdx += startMarker.Length;
        var endIdx = output.IndexOf(endMarker, startIdx);
        if (endIdx < 0) return null;

        return output[startIdx..endIdx].Trim();
    }

    /// <summary>
    /// Locate the ATT.Cli project path relative to the UI host.
    /// </summary>
    private static string GetCliProjectPath()
    {
        var uiDir = AppContext.BaseDirectory;
        // Navigate up from bin/Debug/net9.0 to solution root
        var dir = new DirectoryInfo(uiDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ATT.sln")))
            dir = dir.Parent;

        if (dir != null)
        {
            var cliPath = Path.Combine(dir.FullName, "ATT.Cli", "ATT.Cli.csproj");
            if (File.Exists(cliPath)) return cliPath;
        }

        // Fallback
        return Path.Combine(
            Path.GetDirectoryName(typeof(DeviceSetupViewModel).Assembly.Location)!,
            "..", "..", "..", "..", "ATT.Cli", "ATT.Cli.csproj");
    }
}
