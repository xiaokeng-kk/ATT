using ATT.Cli;
using ATT.Cli.Analyzers;
using ATT.Cli.Models;
using ATT.Cli.Parsers;

namespace ATT.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        var (filePath, dataText, csvOutput, configFile) = ParseArgs(args);

        // 如果有 --config，解析配置文件并自动打开对应设备
        if (!string.IsNullOrEmpty(configFile))
        {
            return RunDeviceMode(configFile);
        }

        return RunAnalyzerMode(filePath, dataText, csvOutput);
    }

    // ===== 设备模式 — 根据配置文件自动解析并打开设备 =====

    private static int RunDeviceMode(string configFile)
    {
        try
        {
            using var deviceService = new DeviceService();
            var devices = deviceService.StartFromConfig(configFile);

            Console.WriteLine($"成功启动 {devices.Count} 个设备，按 Enter 退出...");
            Console.ReadLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
            return 1;
        }
    }

    // ===== 数据分析模式 =====

    private static int RunAnalyzerMode(string? filePath, string? dataText, bool csvOutput)
    {
        try
        {
            PacketData packet;

            if (!string.IsNullOrEmpty(filePath))
            {
                packet = DataParser.ParseFromFile(filePath);
            }
            else if (!string.IsNullOrEmpty(dataText))
            {
                packet = DataParser.Parse(dataText, "command-line");
            }
            else
            {
                // 从标准输入读取（支持管道输入）
                var stdin = Console.In.ReadToEnd();
                if (string.IsNullOrWhiteSpace(stdin))
                {
                    PrintUsage();
                    return 1;
                }
                packet = DataParser.Parse(stdin, "stdin");
            }

            if (packet.SampleCount == 0)
            {
                Console.Error.WriteLine("错误: 未解析到有效数据。");
                return 1;
            }

            var result = FeatureAnalyzer.Analyze(packet);

            if (csvOutput)
                Console.WriteLine(result.ToCsvLine());
            else
                Console.WriteLine(result.ToString());

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
            return 1;
        }
    }

    // ===== 命令行参数解析 =====

    private static (string? file, string? data, bool csv, string? config) ParseArgs(string[] args)
    {
        string? file = null;
        string? data = null;
        bool csv = false;
        string? config = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                case "-f":
                    if (++i < args.Length) file = args[i];
                    break;
                case "--data":
                case "-d":
                    if (++i < args.Length) data = args[i];
                    break;
                case "--csv":
                case "-c":
                    csv = true;
                    break;
                case "--config":
                    if (++i < args.Length) config = args[i];
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
            }
        }
        return (file, data, csv, config);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            ATT.Cli — 报文数据分析与特征值计算工具
            =====================================
            用法:
              ATT.Cli --file <文件路径>    从文件读取数据
              ATT.Cli -f <文件路径>

              ATT.Cli --data <数值列表>    直接传入数据（逗号/空格分隔）
              ATT.Cli -d "1.0 2.0 3.0"

              ATT.Cli                      从标准输入读取（支持管道）
              ATT.Cli --csv                输出为 CSV 格式

              ATT.Cli --config <文件路径>  加载设备配置（JSON），自动解析桥接器/传感器

            数据格式:
              CSV / 空格 / Tab / 分号分隔的数值
              以 # 开头的行被忽略

            示例:
              ATT.Cli -f measurements.csv
              ATT.Cli -d "10.5 20.3 15.8 12.1"
              cat data.txt | ATT.Cli
              ATT.Cli -f data.csv --csv > output.csv
              ATT.Cli --config config/bridge-config.json
            """);
    }
}
