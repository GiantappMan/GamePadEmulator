using System;
using System.ServiceProcess;
using Nefarius.ViGEm.Client;

namespace GamePadEmulator.Core;

/// <summary>
/// High-level façade the UI binds to. Owns the singleton <see cref="ViGEmClient"/>
/// connection, swaps between DS4 and Xbox targets, and reports driver status.
/// </summary>
public sealed class ControllerService : IDisposable
{
    private ViGEmClient? _client;
    private VirtualController? _current;

    /// <summary>True once a ViGEmClient has connected to the ViGEmBus driver.</summary>
    public bool DriverReady => _client != null;

    /// <summary>The currently active virtual controller (null when not created).</summary>
    public VirtualController? Current => _current;

    /// <summary>Currently selected emulation type; the UI sets this before Connect.</summary>
    public ControllerType SelectedType { get; set; } = ControllerType.Xbox;

    /// <summary>
    /// Attempt to open the ViGEmBus driver. Returns false (no throw) when the driver
    /// isn't installed so the UI can surface a friendly install prompt instead of crashing.
    /// </summary>
    public bool TryOpenDriver()
    {
        if (_client != null) return true;
        try
        {
            _client = new ViGEmClient();
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _client = null;
            return false;
        }
    }

    /// <summary>True if the ViGEmBus kernel service is present on this machine.</summary>
    public static bool IsViGEmBusInstalled()
    {
        try
        {
            // The driver registers itself as a service named "ViGEmBus".
            using var sc = new ServiceController("ViGEmBus");
            var _ = sc.Status;   // touch to force a lookup
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Create + connect the virtual controller for <see cref="SelectedType"/>.</summary>
    public bool Connect(out string error)
    {
        error = string.Empty;
        if (!TryOpenDriver())
        {
            error = "ViGEmBus 驱动未安装或未运行。请先安装 ViGEmBus 驱动后再连接。";
            return false;
        }

        try
        {
            _current?.Disconnect();
            _current = SelectedType == ControllerType.PlayStation
                ? new Ds4Controller(_client!)
                : (VirtualController)new XboxController(_client!);
            _current.Connect();
            return true;
        }
        catch (Exception ex)
        {
            _current = null;
            error = $"连接虚拟手柄失败：{ex.Message}";
            return false;
        }
    }

    /// <summary>Push a full state snapshot to the active controller (no-op if not connected).</summary>
    public void SendState(in ControllerState state) => _current?.SendState(in state);

    /// <summary>Disconnect and drop the active controller.</summary>
    public void Disconnect()
    {
        _current?.Disconnect();
        _current = null;
    }

    public void Dispose()
    {
        _current?.Dispose();
        _current = null;
        _client?.Dispose();
        _client = null;
    }
}
