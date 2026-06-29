using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GamePadEmulator.Controls;

/// <summary>
/// A draggable analog thumbstick. The user grabs the stick cap and drags it inside a
/// circular well; the cap is clamped to the well's radius. The normalized position
/// (X right +, Y up +, range -1..+1) is exposed via <see cref="X"/>/<see cref="Y"/>
/// and raised through <see cref="PositionChanged"/> so the host can push it to the
/// virtual controller. A small central deadzone prevents jitter. Clicking inside the
/// well (not on the cap) re-centers the stick.
/// </summary>
[TemplatePart(Name = PartCap, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PartCapHighlight, Type = typeof(FrameworkElement))]
public class AnalogStick : Control
{
    private const string PartCap = "PART_Cap";
    private const string PartCapHighlight = "PART_CapHighlight";

    private FrameworkElement? _cap;
    private FrameworkElement? _highlight;   // optional element that tracks the cap (e.g. gloss)
    private bool _dragging;
    private Point _origin;          // well centre in control coords
    private double _radius;         // max travel radius in DIPs

    static AnalogStick()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AnalogStick),
            new FrameworkPropertyMetadata(typeof(AnalogStick)));
    }

    #region Properties

    public static readonly DependencyProperty XProperty = DependencyProperty.Register(
        nameof(X), typeof(double), typeof(AnalogStick),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPositionChanged));

    public static readonly DependencyProperty YProperty = DependencyProperty.Register(
        nameof(Y), typeof(double), typeof(AnalogStick),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPositionChanged));

    /// <summary>0..1 inner deadzone radius; values below it are snapped to centre.</summary>
    public static readonly DependencyProperty DeadzoneProperty = DependencyProperty.Register(
        nameof(Deadzone), typeof(double), typeof(AnalogStick), new PropertyMetadata(0.08));

    /// <summary>Visual radius (DIPs) the cap travels from centre at full deflection.</summary>
    public static readonly DependencyProperty TravelRadiusProperty = DependencyProperty.Register(
        nameof(TravelRadius), typeof(double), typeof(AnalogStick), new PropertyMetadata(38.0));

    public double X { get => (double)GetValue(XProperty); set => SetValue(XProperty, value); }
    public double Y { get => (double)GetValue(YProperty); set => SetValue(YProperty, value); }
    public double Deadzone { get => (double)GetValue(DeadzoneProperty); set => SetValue(DeadzoneProperty, value); }
    public double TravelRadius { get => (double)GetValue(TravelRadiusProperty); set => SetValue(TravelRadiusProperty, value); }

    #endregion

    /// <summary>Raised whenever X/Y change (drag, programmatic, or re-centre).</summary>
    public event EventHandler<AnalogStickEventArgs>? PositionChanged;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _cap = GetTemplateChild(PartCap) as FrameworkElement;
        _highlight = GetTemplateChild(PartCapHighlight) as FrameworkElement;

        if (_cap != null)
        {
            _cap.MouseDown -= Cap_OnMouseDown;
            _cap.MouseDown += Cap_OnMouseDown;
        }
        // Capture drag originating anywhere in the well (click-to-move).
        MouseDown -= Well_OnMouseDown;
        MouseDown += Well_OnMouseDown;
        UpdateCapPosition();
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var size = base.ArrangeOverride(arrangeBounds);
        _origin = new Point(size.Width / 2.0, size.Height / 2.0);
        _radius = TravelRadius;
        UpdateCapPosition();
        return size;
    }

    private void Well_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        // If the press was on the cap, let Cap_OnMouseDown handle the drag.
        if (e.OriginalSource is DependencyObject d && IsDescendantOf(_cap, d)) return;

        // Click-to-move: jump the cap toward the clicked point (clamped), then allow drag.
        var p = e.GetPosition(this);
        SetFromPoint(p);
        BeginDrag(e);
        e.Handled = true;
    }

    private void Cap_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        BeginDrag(e);
        e.Handled = true;
    }

    private void BeginDrag(MouseButtonEventArgs e)
    {
        _dragging = true;
        CaptureMouse();
        MouseMove -= OnDragMove;
        MouseUp -= OnDragEnd;
        MouseMove += OnDragMove;
        MouseUp += OnDragEnd;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(this);
        SetFromPoint(p);
    }

    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        MouseMove -= OnDragMove;
        MouseUp -= OnDragEnd;
        // Release-to-centre for a spring-loaded feel.
        X = 0; Y = 0;
    }

    /// <summary>Map a pointer position (relative to this control) to clamped X/Y.</summary>
    private void SetFromPoint(Point p)
    {
        double dx = p.X - _origin.X;
        double dy = p.Y - _origin.Y;     // screen Y grows downward
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double max = Math.Max(1.0, _radius);

        // Clamp to circle.
        if (dist > max)
        {
            dx = dx / dist * max;
            dy = dy / dist * max;
            dist = max;
        }

        double nx = dx / max;            // -1..+1 (right +)
        double ny = -dy / max;           // -1..+1 (up +)

        // Deadzone: scale so values just outside the zone map to ~ the zone edge.
        double mag = Math.Sqrt(nx * nx + ny * ny);
        if (mag < Deadzone)
        {
            nx = 0; ny = 0;
        }
        else
        {
            double scaled = (mag - Deadzone) / (1.0 - Deadzone);
            nx = nx / mag * Math.Min(1.0, scaled);
            ny = ny / mag * Math.Min(1.0, scaled);
        }

        X = Math.Round(nx, 3);
        Y = Math.Round(ny, 3);
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnalogStick s)
        {
            s.UpdateCapPosition();
            s.PositionChanged?.Invoke(s, new AnalogStickEventArgs(s.X, s.Y));
        }
    }

    /// <summary>Push the cap visual (and any tracking highlight) to the current X/Y.</summary>
    private void UpdateCapPosition()
    {
        if (_cap == null || _origin == default && !IsLoaded) return;
        if (_radius <= 0) _radius = TravelRadius;
        double offsetX = X * _radius;
        double offsetY = -Y * _radius;     // invert: +Y up
        var tt = new TranslateTransform(offsetX, offsetY);
        _cap.RenderTransform = tt;
        // Keep the gloss/highlight glued to the cap so it moves with it.
        if (_highlight != null)
            _highlight.RenderTransform = new TranslateTransform(offsetX, offsetY);
    }

    private static bool IsDescendantOf(DependencyObject? ancestor, DependencyObject node)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor)) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }
}

public sealed class AnalogStickEventArgs(double x, double y) : EventArgs
{
    public double X { get; } = x;
    public double Y { get; } = y;
}
