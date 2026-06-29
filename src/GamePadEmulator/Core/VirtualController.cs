using System;

namespace GamePadEmulator.Core;

/// <summary>
/// Common contract every controller backend (DS4 / Xbox) must implement.
/// The UI talks to this interface only, so switching protocols is a pure
/// composition step in <see cref="ControllerService"/>.
/// </summary>
public interface VirtualController : IDisposable
{
    ControllerType Type { get; }
    bool IsConnected { get; }

    /// <summary>Plug the virtual device into the OS. Throws if the driver is unavailable.</summary>
    void Connect();

    /// <summary>Unplug the virtual device. Safe to call repeatedly.</summary>
    void Disconnect();

    /// <summary>Push a full input snapshot to the virtual device.</summary>
    void SendState(in ControllerState state);

    /// <summary>Release all inputs (centered sticks, released triggers/buttons).</summary>
    void Reset();
}

/// <summary>
/// Shared axis math used by both backends. The UI produces normalized doubles;
/// each backend quantizes them to the integer range its HID report expects.
/// </summary>
internal static class AxisMath
{
    /// <summary>Map a normalized [-1,+1] double to an unsigned byte [0..255] (128 = center).</summary>
    public static byte ToByte8(double v)
    {
        v = Math.Clamp(v, -1.0, 1.0);
        int i = (int)Math.Round((v + 1.0) * 127.5);
        return (byte)Math.Clamp(i, 0, 255);
    }

    /// <summary>Map a normalized [0,1] trigger double to an unsigned byte [0..255].</summary>
    public static byte TriggerToByte8(double v)
    {
        v = Math.Clamp(v, 0.0, 1.0);
        return (byte)Math.Round(v * 255.0);
    }

    /// <summary>Map a normalized [-1,+1] double to a signed Int16 [-32768..32767].</summary>
    public static short ToInt16(double v)
    {
        v = Math.Clamp(v, -1.0, 1.0);
        int i = (int)Math.Round(v * 32767.0);
        return (short)Math.Clamp(i, short.MinValue, short.MaxValue);
    }

    /// <summary>Map a normalized [0,1] trigger double to a signed Int16 [0..32767].</summary>
    public static short TriggerToInt16(double v)
    {
        v = Math.Clamp(v, 0.0, 1.0);
        return (short)Math.Round(v * 32767.0);
    }
}
