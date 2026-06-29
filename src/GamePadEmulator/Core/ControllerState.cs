namespace GamePadEmulator.Core;

/// <summary>
/// Aggregated, controller-agnostic snapshot of every input on the virtual pad.
/// Values are normalized: axes [-1.0, +1.0], triggers [0.0, 1.0], buttons boolean.
/// This single struct makes the UI binding layer and the two controller backends
/// (DS4 / Xbox) independent of each other.
/// </summary>
public struct ControllerState
{
    // Analog sticks, range -1.0 .. +1.0 (X right, Y down to match screen coords).
    public double LeftStickX;
    public double LeftStickY;
    public double RightStickX;
    public double RightStickY;

    // Triggers, range 0.0 (released) .. 1.0 (fully pressed).
    public double LeftTrigger;
    public double RightTrigger;

    // Digital face buttons + bumpers.
    public bool A;          // Xbox A  / PS Cross
    public bool B;          // Xbox B  / PS Circle
    public bool X;          // Xbox X  / PS Square
    public bool Y;          // Xbox Y  / PS Triangle
    public bool LeftBumper;
    public bool RightBumper;
    public bool LeftThumb;
    public bool RightThumb;

    // Special buttons.
    public bool Back;       // Xbox Back / PS Share
    public bool Start;      // Xbox Start / PS Options
    public bool Guide;      // Xbox Guide / PS PS button
    public bool BigButton;  // Xbox "BigButton"
    public bool Touchpad;   // DS4 touchpad click (no Xbox equivalent)

    // D-pad, four directional booleans (independent of stick).
    public bool DPadUp;
    public bool DPadDown;
    public bool DPadLeft;
    public bool DPadRight;

    /// <summary>Neutral state: sticks centered, triggers released, no buttons.</summary>
    public static readonly ControllerState Default = new()
    {
        LeftStickX = 0, LeftStickY = 0,
        RightStickX = 0, RightStickY = 0,
        LeftTrigger = 0, RightTrigger = 0
    };
}

/// <summary>Which physical/virtual controller protocol to emulate.</summary>
public enum ControllerType
{
    PlayStation,  // DualShock 4 (DS4) -> DirectInput + DS4 HID
    Xbox          // Xbox 360 controller -> XInput
}

/// <summary>Flattened identifier for any single input used by the UI for hit-testing.</summary>
public enum InputId
{
    LeftStick, RightStick, LeftStickButton, RightStickButton,
    LeftTrigger, RightTrigger, LeftBumper, RightBumper,
    A, B, X, Y, Up, Down, Left, Right,
    Back, Start, Guide, BigButton,
    Touchpad
}
