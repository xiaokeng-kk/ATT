namespace ATT.Core.Models;

/// <summary>
/// CAN 数据帧
/// </summary>
public class CanFrame
{
    /// <summary>CAN ID (标准帧 11-bit / 扩展帧 29-bit)</summary>
    public int Id { get; init; }

    /// <summary>帧格式：标准帧或扩展帧</summary>
    public CanFrameFormat FrameFormat { get; init; }

    /// <summary>数据载荷 (通常 0~8 字节)</summary>
    public byte[] Data { get; init; } = [];

    public override string ToString()
    {
        var dataStr = string.Join(" ", Data.Select(b => $"{b:X2}"));
        return $"ID=0x{Id:X}{(FrameFormat == CanFrameFormat.Extended ? " (Extended)" : "")} Data=[{dataStr}]";
    }
}
