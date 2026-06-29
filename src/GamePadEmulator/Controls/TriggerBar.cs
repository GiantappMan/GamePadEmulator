using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GamePadEmulator.Controls;

/// <summary>
/// An analog trigger (L2/R2 / LT/RT). Press-and-hold over the bar ramps the value
/// from 0 to 1; releasing returns it to 0. Exposes <see cref="Value"/> in 0..1 and
/// raises <see cref="ValueChanged"/>. A mouse-wheel scroll also nudges the value,
/// letting users set partial pulls precisely.
/// </summary>
[TemplatePart(Name = PartFill, Type = typeof(FrameworkElement))]
public class TriggerBar : Control
{
    private const string PartFill = "PART_Fill";
    private FrameworkElement? _fill;
    private bool _pressing;

    static TriggerBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TriggerBar),
            new FrameworkPropertyMetadata(typeof(TriggerBar)));
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(TriggerBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public event EventHandler<TriggerEventArgs>? ValueChanged;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _fill = GetTemplateChild(PartFill) as FrameworkElement;
        UpdateFill();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _pressing = true;
        CaptureMouse();
        UpdateFromMouse(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pressing) UpdateFromMouse(e.GetPosition(this));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _pressing = false;
        ReleaseMouseCapture();
        Value = 0; // spring back
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        Value = Math.Clamp(Value + (e.Delta > 0 ? 0.1 : -0.1), 0.0, 1.0);
        e.Handled = true;
    }

    private void UpdateFromMouse(Point p)
    {
        double h = Math.Max(1, ActualHeight);
        // 0 at bottom, 1 at top: invert because screen Y grows downward.
        double v = Math.Clamp(1.0 - (p.Y / h), 0.0, 1.0);
        Value = Math.Round(v, 3);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TriggerBar t)
        {
            t.UpdateFill();
            t.ValueChanged?.Invoke(t, new TriggerEventArgs(t.Value));
        }
    }

    private void UpdateFill()
    {
        if (_fill == null) return;
        _fill.Height = Math.Max(0, ActualHeight * Value);
        if (double.IsNaN(_fill.Height)) _fill.Height = 0;
    }
}

public sealed class TriggerEventArgs(double value) : EventArgs
{
    public double Value { get; } = value;
}
