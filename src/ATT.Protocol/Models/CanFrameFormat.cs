namespace ATT.Protocol.Models;

/// <summary>
/// CAN 帧格式
/// </summary>
public enum CanFrameFormat : byte
{
    /// <summary>标准帧 (Standard) — 11-bit ID</summary>
    Standard = 0x00,

    /// <summary>扩展帧 (Extended) — 29-bit ID</summary>
    Extended = 0x01,
}
