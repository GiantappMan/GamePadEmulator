using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;

namespace GamePadEmulator.Core;

/// <summary>
/// Virtual DualShock 4 (PlayStation) controller backed by the ViGEmBus DS4 target.
/// DS4 HID byte conventions: axes are 0..255 (128 = centre). For Y, 0 = up and
/// 255 = down, so the normalized "+Y is up" state value is negated before quantizing.
/// Triggers are 0..255 sliders. The D-pad is a single 8-direction field.
/// </summary>
internal sealed class Ds4Controller : VirtualController
{
    private readonly ViGEmClient _client;
    private readonly IDualShock4Controller _pad;
    private bool _connected;

    public Ds4Controller(ViGEmClient client)
    {
        _client = client;
        // Official Sony DS4 Vendor/Product IDs so games that match hardware IDs see a DS4.
        _pad = client.CreateDualShock4Controller(
            /*VID_Sony*/   0x054C,
            /*PID_DS4*/    0x09CC);
        // Batch all Set* calls into one report submission for atomic, low-overhead updates.
        _pad.AutoSubmitReport = false;
    }

    public ControllerType Type => ControllerType.PlayStation;
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

        // --- Axes (8-bit, centre = 128; Y inverted because DS4 Y grows downward). ---
        _pad.SetAxisValue(DualShock4Axis.LeftThumbX,  AxisMath.ToByte8(s.LeftStickX));
        _pad.SetAxisValue(DualShock4Axis.LeftThumbY,  AxisMath.ToByte8(-s.LeftStickY));
        _pad.SetAxisValue(DualShock4Axis.RightThumbX, AxisMath.ToByte8(s.RightStickX));
        _pad.SetAxisValue(DualShock4Axis.RightThumbY, AxisMath.ToByte8(-s.RightStickY));

        // --- Triggers (L2 / R2) as 8-bit sliders. ---
        _pad.SetSliderValue(DualShock4Slider.LeftTrigger,  AxisMath.TriggerToByte8(s.LeftTrigger));
        _pad.SetSliderValue(DualShock4Slider.RightTrigger, AxisMath.TriggerToByte8(s.RightTrigger));

        // --- Digital buttons (face + shoulders + thumbs + share/options). ---
        _pad.SetButtonState(DualShock4Button.Cross,        s.A);          // PS ✕
        _pad.SetButtonState(DualShock4Button.Circle,       s.B);          // PS ○
        _pad.SetButtonState(DualShock4Button.Square,       s.X);          // PS □
        _pad.SetButtonState(DualShock4Button.Triangle,     s.Y);          // PS △
        _pad.SetButtonState(DualShock4Button.ShoulderLeft, s.LeftBumper); // L1
        _pad.SetButtonState(DualShock4Button.ShoulderRight,s.RightBumper);// R1
        _pad.SetButtonState(DualShock4Button.ThumbLeft,    s.LeftThumb);  // L3
        _pad.SetButtonState(DualShock4Button.ThumbRight,   s.RightThumb); // R3
        _pad.SetButtonState(DualShock4Button.Share,        s.Back);
        _pad.SetButtonState(DualShock4Button.Options,      s.Start);

        // --- Special buttons: PS button (bit 0x01) + touchpad click (bit 0x02). ---
        byte special = 0;
        if (s.Guide) special |= 0x01;            // PS home button
        if (s.Touchpad) special |= 0x02;         // touchpad click
        _pad.SetSpecialButtonsFull(special);

        // --- D-pad: 4 booleans -> single 8-direction value. ---
        _pad.SetDPadDirection(ToDpad(s.DPadUp, s.DPadDown, s.DPadLeft, s.DPadRight));

        _pad.SubmitReport();
    }

    public void Reset()
    {
        if (!_connected) return;
        _pad.ResetReport();
        _pad.SubmitReport();
    }

    private static DualShock4DPadDirection ToDpad(bool up, bool down, bool left, bool right) => (up, down, left, right) switch
    {
        (true,  false, false, false) => DualShock4DPadDirection.North,
        (true,  false, false, true ) => DualShock4DPadDirection.Northeast,
        (false, false, false, true ) => DualShock4DPadDirection.East,
        (false, true,  false, true ) => DualShock4DPadDirection.Southeast,
        (false, true,  false, false) => DualShock4DPadDirection.South,
        (false, true,  true,  false) => DualShock4DPadDirection.Southwest,
        (false, false, true,  false) => DualShock4DPadDirection.West,
        (true,  false, true,  false) => DualShock4DPadDirection.Northwest,
        _ => DualShock4DPadDirection.None,
    };

    public void Dispose()
    {
        Disconnect();
    }
}
