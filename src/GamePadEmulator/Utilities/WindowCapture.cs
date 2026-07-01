using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GamePadEmulator.Utilities;

/// <summary>
/// Captures the visual tree of a WPF window to a PNG. This is run from inside
/// the app process (where we own the foreground), so it doesn't have to fight
/// the window manager and works reliably for hardware-accelerated WPF content.
/// </summary>
public static class WindowCapture
{
    /// <summary>Capture <paramref name="element"/> to <paramref name="path"/> after <paramref name="delayMs"/>.</summary>
    public static void CaptureDelayed(FrameworkElement element, string path, int delayMs = 1200)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try { Save(element, path); }
            catch (Exception ex) { File.WriteAllText(path + ".err", ex.ToString()); }
            // Signal the harness that the capture is done.
            File.WriteAllText(path + ".done", DateTime.UtcNow.ToString("o"));
        };
        timer.Start();
    }

    private static void Save(FrameworkElement element, string path)
    {
        element.ApplyTemplate();
        element.UpdateLayout();
        // 注意：绝不能用 Measure(infinity) 强制重测——那会让 Viewbox/UserControl 等依赖
        // 有限父容器约束的缩放逻辑失效（手柄会被按原始固定尺寸渲染而溢出）。
        // 直接读取已由真实窗口布局计算好的 ActualWidth/Height，并据此 Arrange 一次确保渲染。
        var size = new Size(
            element.ActualWidth > 0 ? element.ActualWidth : element.Width,
            element.ActualHeight > 0 ? element.ActualHeight : element.Height);
        if (size.Width <= 0 || size.Height <= 0) size = new Size(1040, 720);
        element.Arrange(new Rect(new Point(), size));

        var dpi = VisualTreeHelper.GetDpi(element);
        int w = (int)(size.Width * dpi.DpiScaleX);
        int h = (int)(size.Height * dpi.DpiScaleY);

        var rtb = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        rtb.Render(element);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
