using System.Windows;
using System.Windows.Media;

namespace GamePadEmulator.Views;

/// <summary>
/// Shared helpers for the on-screen controller views.
/// </summary>
internal static class ViewHelper
{
    /// <summary>
    /// Walk up the visual/logical tree from <paramref name="source"/> to the first
    /// <see cref="FrameworkElement"/> whose <see cref="FrameworkElement.Tag"/> is a
    /// non-empty string. This lets one top-level PreviewMouse handler map any deeply
    /// nested click (a glyph Path inside a Button inside a Grid…) back to its
    /// semantic input id.
    /// </summary>
    public static string? ResolveTag(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement fe && fe.Tag is string s && !string.IsNullOrEmpty(s))
                return s;
            // Prefer visual parent, fall back to logical parent for content elements.
            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
}
