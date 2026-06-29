using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GamePadEmulator.Controls;
using GamePadEmulator.Core;

namespace GamePadEmulator.Views;

/// <summary>
/// DualShock 4 (PlayStation) on-screen controller. All interactive elements route
/// through <see cref="PushState"/> which calls into the shared <see cref="App.Controllers"/>.
/// Holds the live <see cref="ControllerState"/> and re-sends it on every input change
/// so held buttons + stick positions compose correctly.
/// </summary>
public partial class Ds4View : UserControl, IGamepadView
{
    private ControllerState _state = ControllerState.Default;

    public Ds4View()
    {
        InitializeComponent();
    }

    /// <summary>Flush the current state to the virtual controller.</summary>
    public void PushState() => App.Controllers.SendState(in _state);

    /// <summary>Reset every input to neutral and send.</summary>
    public void Reset()
    {
        _state.LeftStickX = _state.LeftStickY = 0;
        _state.RightStickX = _state.RightStickY = 0;
        _state.LeftTrigger = _state.RightTrigger = 0;
        LeftStick.X = LeftStick.Y = 0;
        RightStick.X = RightStick.Y = 0;
        _state.A = _state.B = _state.X = _state.Y = false;
        _state.LeftBumper = _state.RightBumper = false;
        _state.LeftThumb = _state.RightThumb = false;
        _state.Back = _state.Start = _state.Guide = _state.Touchpad = false;
        _state.DPadUp = _state.DPadDown = _state.DPadLeft = _state.DPadRight = false;
        PushState();
    }

    // ---------- analog ----------
    private void LeftStick_Changed(object? sender, AnalogStickEventArgs e)
    { _state.LeftStickX = e.X; _state.LeftStickY = e.Y; PushState(); }

    private void RightStick_Changed(object? sender, AnalogStickEventArgs e)
    { _state.RightStickX = e.X; _state.RightStickY = e.Y; PushState(); }

    private void LeftTrigger_Changed(object? sender, TriggerEventArgs e)
    { _state.LeftTrigger = e.Value; PushState(); }

    private void RightTrigger_Changed(object? sender, TriggerEventArgs e)
    { _state.RightTrigger = e.Value; PushState(); }

    // ---------- digital buttons ----------
    // One top-level PreviewMouse handler: resolve the semantic input id by walking
    // up from the clicked element to the nearest tagged ancestor.
    private void Btn_PreviewDown(object sender, MouseButtonEventArgs e)
    {
        var id = ViewHelper.ResolveTag(e.OriginalSource as DependencyObject);
        ApplyDigital(id, true);
    }

    private void Btn_PreviewUp(object sender, MouseButtonEventArgs e)
    {
        var id = ViewHelper.ResolveTag(e.OriginalSource as DependencyObject);
        ApplyDigital(id, false);
    }

    private void ApplyDigital(string? id, bool pressed)
    {
        switch (id)
        {
            case "A": _state.A = pressed; break;
            case "B": _state.B = pressed; break;
            case "X": _state.X = pressed; break;
            case "Y": _state.Y = pressed; break;
            case "LB": _state.LeftBumper = pressed; break;
            case "RB": _state.RightBumper = pressed; break;
            case "L3": _state.LeftThumb = pressed; break;
            case "R3": _state.RightThumb = pressed; break;
            case "Back": _state.Back = pressed; break;
            case "Start": _state.Start = pressed; break;
            case "Guide": _state.Guide = pressed; break;
            case "Touch": _state.Touchpad = pressed; break;
            case "Up": _state.DPadUp = pressed; break;
            case "Down": _state.DPadDown = pressed; break;
            case "Left": _state.DPadLeft = pressed; break;
            case "Right": _state.DPadRight = pressed; break;
        }
        PushState();
    }
}

/// <summary>Shared contract for PS and Xbox views so the shell can drive either.</summary>
public interface IGamepadView
{
    void PushState();
    void Reset();
}
