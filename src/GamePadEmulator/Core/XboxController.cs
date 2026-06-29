using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace GamePadEmulator.Core;

/// <summary>
/// Virtual Xbox 360 controller backed by the ViGEmBus XUSB target.
/// XInput HID conventions: axes are signed Int16 (-32768..+32767, +Y is up);
/// triggers are 0..255 sliders; the D-pad is exposed as four discrete buttons.
/// XInput is what the vast majority of PC games read, so this is the most
/// compatible choice for older/Windows-store titles.
/// </summary>
internal sealed class XboxController : VirtualController
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _pad;
    private bool _connected;

    public XboxController(ViGEmClient client)
    {
        _client = client;
        // Default ViGEm Xbox vendor/product IDs (no override needed for XInput compatibility).
        _pad = client.CreateXbox360Controller();
        _pad.AutoSubmitReport = false;
    }

    public ControllerType Type => ControllerType.Xbox;
    public bool IsConnected => _connected;

    public void Connect()
    {
        if (_connected) return;
        _pad.Connect();
        _connected = true;
        Reset();
    }

    public void Disconnect()
    {
        if (!_connected) return;
        try { Reset(); } catch { /* ignore reset errors on teardown */ }
        _pad.Disconnect();
        _connected = false;
    }

    public void SendState(in ControllerState s)
    {
        if (!_connected) return;

        // --- Axes (Int16; +Y up). UI sends +Y up so no inversion needed. ---
        _pad.SetAxisValue(Xbox360Axis.LeftThumbX,  AxisMath.ToInt16(s.LeftStickX));
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY,  AxisMath.ToInt16(s.LeftStickY));
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, AxisMath.ToInt16(s.RightStickX));
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, AxisMath.ToInt16(s.RightStickY));

        // --- Triggers (0..255). ---
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger,  AxisMath.TriggerToByte8(s.LeftTrigger));
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, AxisMath.TriggerToByte8(s.RightTrigger));

        // --- Face + shoulder + thumb digital buttons. ---
        _pad.SetButtonState(Xbox360Button.A,            s.A);
        _pad.SetButtonState(Xbox360Button.B,            s.B);
        _pad.SetButtonState(Xbox360Button.X,            s.X);
        _pad.SetButtonState(Xbox360Button.Y,            s.Y);
        _pad.SetButtonState(Xbox360Button.LeftShoulder, s.LeftBumper);
        _pad.SetButtonState(Xbox360Button.RightShoulder,s.RightBumper);
        _pad.SetButtonState(Xbox360Button.LeftThumb,    s.LeftThumb);
        _pad.SetButtonState(Xbox360Button.RightThumb,   s.RightThumb);

        // --- Meta buttons. ---
        _pad.SetButtonState(Xbox360Button.Back,  s.Back);
        _pad.SetButtonState(Xbox360Button.Start, s.Start);
        _pad.SetButtonState(Xbox360Button.Guide, s.Guide);

        // --- D-pad as discrete buttons (XInput style). ---
        _pad.SetButtonState(Xbox360Button.Up,    s.DPadUp);
        _pad.SetButtonState(Xbox360Button.Down,  s.DPadDown);
        _pad.SetButtonState(Xbox360Button.Left,  s.DPadLeft);
        _pad.SetButtonState(Xbox360Button.Right, s.DPadRight);

        _pad.SubmitReport();
    }

    public void Reset()
    {
        if (!_connected) return;
        _pad.ResetReport();
        _pad.SubmitReport();
    }

    public void Dispose()
    {
        Disconnect();
    }
}
