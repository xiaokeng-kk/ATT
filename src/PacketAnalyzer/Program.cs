using PacketAnalyzer;
using PacketAnalyzer.Analyzers;
using PacketAnalyzer.Models;
using PacketAnalyzer.Parsers;

namespace PacketAnalyzer;

internal static class Program
{
    private static int Main(string[] args)
    {
        var (filePath, dataText, csvOutput, configFile) = ParseArgs(args);

        // 如果有 --config，启动桥接器
        if (!string.IsNullOrEmpty(configFile))
        {
            return RunBridgeMode(configFile);
        }

        return RunAnalyzerMode(filePath, dataText, csvOutput);
    }

    // ===== 桥接器模式 =====

    private static int RunBridgeMode(string configFile)
    {
        try
        {
            using var bridgeService = new BridgeService();
            var bridges = bridgeService.StartFromConfig(configFile);

            Console.WriteLine($"成功启动 {bridges.Count} 个桥接器，按 Enter 退出...");
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
            PacketAnalyzer — 报文数据分析与特征值计算工具
            ===============================================
            用法:
              PacketAnalyzer --file <文件路径>    从文件读取数据
              PacketAnalyzer -f <文件路径>

              PacketAnalyzer --data <数值列表>    直接传入数据（逗号/空格分隔）
              PacketAnalyzer -d "1.0 2.0 3.0"

              PacketAnalyzer                      从标准输入读取（支持管道）
              PacketAnalyzer --csv                输出为 CSV 格式

              PacketAnalyzer --config <文件路径>  启动桥接器（JSON 配置文件）

            数据格式:
              CSV / 空格 / Tab / 分号分隔的数值
              以 # 开头的行被忽略

            示例:
              PacketAnalyzer -f measurements.csv
              PacketAnalyzer -d "10.5 20.3 15.8 12.1"
              cat data.txt | PacketAnalyzer
              PacketAnalyzer -f data.csv --csv > output.csv
              PacketAnalyzer --config config/bridge-config.json
            """);
    }
}
