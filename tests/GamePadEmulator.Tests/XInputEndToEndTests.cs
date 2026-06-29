using System.ComponentModel;
using System.Runtime.InteropServices;
using GamePadEmulator.Core;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Xunit;

// XInput user-index 0 is a shared physical slot: only one virtual pad can own it at a time,
// so the device/xinput tests must not run in parallel against each other.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace GamePadEmulator.Tests;

/// <summary>
/// True end-to-end test of the input-injection pipeline. It uses the REAL XboxController
/// backend to create a virtual Xbox 360 pad, injects known inputs through SendState, and
/// reads them back through the Windows XInput API — the same API games use. This proves
/// the full chain: UI state → ViGEm SubmitReport → ViGEmBus driver → OS → XInput.
///
/// Requires the ViGEmBus driver installed + running. Skips gracefully otherwise so the
/// build never fails on a driver-less machine, but on a driver-equipped machine it must pass.
/// </summary>
public class XInputEndToEndTests
{
    private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;

    // ----- XInput P/Invoke (XINPUT_1_4.dll, the same module games use) -----
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    // XInput 9.1.0 is the OS-boxed, always-present redistributable on all modern Windows
    // (ships as xinput9_1_0.dll in System32). More reliable for readback than XINPUT_1_4.dll,
    // which may require the legacy DirectX SDK redistributable.
    [DllImport("xinput9_1_0.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

    // XINPUT button bit masks
    private const ushort XINPUT_GAMEPAD_A = 0x1000;
    private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;

    private static bool IsDriverAvailable()
    {
        try { _ = new ViGEmClient(); return true; }
        catch { return false; }
    }

    [Fact]
    public void XboxController_A_button_and_stick_reach_XInput()
    {
        if (!IsDriverAvailable())
        {
            // Not a failure of the code — the kernel driver isn't on this machine.
            // Skip rather than assert, so CI/developer machines without the driver still build green.
            return;
        }

        using var client = new ViGEmClient();
        var backend = new XboxController(client);

        try
        {
            backend.Connect();

            // --- 1. Inject: A button held + left stick pushed fully right + right trigger half ---
            var s = new ControllerState
            {
                A = true,
                LeftStickX = 1.0,      // full right
                LeftStickY = 0.0,
                RightTrigger = 0.5,
                DPadUp = true,
            };
            backend.SendState(in s);

            // Give the driver a moment to propagate to XInput.
            System.Threading.Thread.Sleep(150);

            // --- 2. Read back via XInput (the exact path games take) ---
            uint err = XInputGetState(0, out var state);
            Assert.NotEqual(ERROR_DEVICE_NOT_CONNECTED, err);   // virtual pad must be visible
            Assert.Equal(0u, err);                              // XINPUT_ERROR_SUCCESS

            // dwPacketNumber changes only when the state changes — must be non-zero (i.e. live data).
            Assert.True(state.dwPacketNumber != 0, "XInput reports no state changes (packet=0)");

            var g = state.Gamepad;
            Assert.True((g.wButtons & XINPUT_GAMEPAD_A) != 0,
                $"A button not seen by XInput. wButtons=0x{g.wButtons:X4}");
            Assert.True((g.wButtons & XINPUT_GAMEPAD_DPAD_UP) != 0,
                $"D-pad up not seen by XInput. wButtons=0x{g.wButtons:X4}");
            Assert.InRange(g.sThumbLX, 30000, short.MaxValue);  // ~32767 full right
            Assert.InRange(g.bRightTrigger, 110, 130);          // ~127 = half pull

            // --- 3. Release and confirm the button clears ---
            s.A = false;
            s.LeftStickX = 0;
            s.RightTrigger = 0;
            s.DPadUp = false;
            backend.SendState(in s);
            System.Threading.Thread.Sleep(150);
            XInputGetState(0, out var after);
            Assert.True((after.Gamepad.wButtons & XINPUT_GAMEPAD_A) == 0,
                "A button still seen after release");
        }
        finally
        {
            backend.Disconnect();
        }
    }
}
