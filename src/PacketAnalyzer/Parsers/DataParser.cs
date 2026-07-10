using PacketAnalyzer.Models;

namespace PacketAnalyzer.Parsers;

/// <summary>
/// 报文数据解析器 — 支持 CSV、空格分隔、逗号分隔等多种格式
/// </summary>
public static class DataParser
{
    /// <summary>
    /// 从文本字符串解析数据
    /// </summary>
    public static PacketData Parse(string text, string source = "stdin")
    {
        var values = new List<double>();
        using var reader = new StringReader(text);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            // 支持逗号、空格、Tab、分号作为分隔符
            var parts = line.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (double.TryParse(part, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    values.Add(value);
                }
            }
        }

        return new PacketData { Values = values, Source = source };
    }

    /// <summary>
    /// 从文件解析数据
    /// </summary>
    public static PacketData ParseFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"文件未找到: {filePath}");

        var text = File.ReadAllText(filePath);
        return Parse(text, filePath);
    }
}
