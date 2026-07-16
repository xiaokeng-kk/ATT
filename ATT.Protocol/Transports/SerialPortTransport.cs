using System.IO.Ports;
using ATT.Core.Base;
using ATT.Core.Models;

namespace ATT.Protocol.Transports;

/// <summary>
/// 串口传输层 — 基于 System.IO.Ports.SerialPort 的 ITransport 实现
/// </summary>
public class SerialPortTransport : UartTransport
{
    private SerialPort? _port;
    private readonly object _lock = new();

    /// <summary>
    /// 使用指定配置创建串口传输层
    /// </summary>
    public SerialPortTransport(UartConfig config) : base(config)
    {
        Name = $"SerialPort ({config.PortName})";
    }

    /// <summary>
    /// 使用端口名和波特率快速创建
    /// </summary>
    public SerialPortTransport(string portName, int baudRate = 1500000/*, int timeoutMs = 1000*/)
        : this(new UartConfig
        {
            PortName = portName,
            BaudRate = baudRate,
            //TimeoutMs = timeoutMs
        })
    {
    }

    /// <summary>打开串口</summary>
    public override bool Open()
    {
        lock (_lock)
        {
            if (IsConnected) return true;

            try
            {
                _port = new SerialPort(Config.PortName, Config.BaudRate)
                {
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    //Handshake = Handshake.None,
                    //ReadTimeout = Config.TimeoutMs,
                    //WriteTimeout = Config.TimeoutMs,
                    //DtrEnable = true,
                    //RtsEnable = true
                };

                _port.DataReceived += OnPortDataReceived;
                _port.ErrorReceived += OnPortErrorReceived;
                _port.Open();

                IsConnected = _port.IsOpen;
                return IsConnected;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{Name}] 打开串口失败: {ex.Message}");
                IsConnected = false;
                return false;
            }
        }
    }

    /// <summary>关闭串口</summary>
    public override bool Close()
    {
        lock (_lock)
        {
            if (!IsConnected) return true;

            try
            {
                if (_port != null)
                {
                    _port.DataReceived -= OnPortDataReceived;
                    _port.ErrorReceived -= OnPortErrorReceived;

                    if (_port.IsOpen)
                        _port.Close();

                    _port.Dispose();
                    _port = null;
                }

                IsConnected = false;
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{Name}] 关闭串口失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>发送原始数据</summary>
    public override void SendRaw(byte[] data)
    {
        if (_port?.IsOpen != true)
            throw new InvalidOperationException($"串口 {Config.PortName} 未打开");

        _port.Write(data, 0, data.Length);
    }

    // ==================== 事件处理 ====================

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var port = (SerialPort)sender;
            int bytesToRead = port.BytesToRead;
            if (bytesToRead <= 0) return;

            byte[] buffer = new byte[bytesToRead];
            port.Read(buffer, 0, bytesToRead);
            OnDataReceived(buffer);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{Name}] 接收数据异常: {ex.Message}");
        }
    }

    private void OnPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        Console.Error.WriteLine($"[{Name}] 串口错误: {e.EventType}");
        // 发生严重错误时尝试重连
        if (e.EventType == SerialError.TXFull || e.EventType == SerialError.Overrun)
        {
            Task.Run(() =>
            {
                Close();
                Thread.Sleep(100);
                Open();
            });
        }
    }

    // ==================== 资源释放 ====================

    /// <summary>
    /// 释放串口资源
    /// </summary>
    public override void Dispose()
    {
        Close();
        base.Dispose();
    }
}
