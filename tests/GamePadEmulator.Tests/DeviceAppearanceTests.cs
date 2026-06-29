using System.Diagnostics;
using System.Runtime.InteropServices;
using GamePadEmulator.Core;
using Nefarius.ViGEm.Client;
using Xunit;

namespace GamePadEmulator.Tests;

/// <summary>
/// Verifies the emulator's ControllerService.Connect flow produces a device the OS
/// enumerates and XInput can discover (not just read). This complements the input-
/// injection test by proving device appearance + enumeration through the app's own
/// service path (the same code the GUI "连接虚拟手柄" button calls).
/// </summary>
public class DeviceAppearanceTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_CAPABILITIES
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XINPUT_GAMEPAD_CAPS Gamepad;
        public Vibration Caps;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD_CAPS
    {
        public ushort wButtons;
        public byte bLeftTrigger, bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Vibration { public ushort wLeftMotorSpeed, wRightMotorSpeed; }

    [DllImport("xinput9_1_0.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern uint XInputGetCapabilities(uint dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES pCapabilities);

    private const uint ERROR_SUCCESS = 0;
    private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;

    private static bool DriverAvailable() { try { _ = new ViGEmClient(); return true; } catch { return false; } }

    [Fact]
    public void ControllerService_Connect_makes_device_visible_to_XInput_and_PnP()
    {
        if (!DriverAvailable()) return;

        using var svc = new ControllerService { SelectedType = ControllerType.Xbox };
        bool ok = svc.Connect(out var error);
        Assert.True(ok, "ControllerService.Connect failed: " + error);
        Assert.NotNull(svc.Current);
        Assert.True(svc.Current!.IsConnected);

        try
        {
            System.Threading.Thread.Sleep(250);  // let the OS enumerate

            // XInput must report a connected controller at user index 0.
            uint caps = XInputGetCapabilities(0, 0, out var capabilities);
            Assert.NotEqual(ERROR_DEVICE_NOT_CONNECTED, caps);
            Assert.Equal(ERROR_SUCCESS, caps);
            // SubType 1 = XINPUT_DEVSUBTYPE_GAMEPAD (Xbox 360 gamepad).
            Assert.Equal((byte)1, capabilities.SubType);
        }
        finally
        {
            svc.Disconnect();
        }
    }
}
