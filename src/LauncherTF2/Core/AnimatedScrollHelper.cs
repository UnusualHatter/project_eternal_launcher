using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LauncherTF2.Core;

/// <summary>
/// Smoothly animates a <see cref="ScrollViewer"/> to a target vertical offset.
/// WPF's <c>ScrollViewer.VerticalOffset</c> is read-only and can't be animated
/// directly — this helper exposes a writable attached DP that proxies through
/// to <see cref="ScrollViewer.ScrollToVerticalOffset(double)"/>.
/// </summary>
public static class AnimatedScrollHelper
{
    public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "AnimatedVerticalOffset",
            typeof(double),
            typeof(AnimatedScrollHelper),
            new UIPropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

    public static double GetAnimatedVerticalOffset(DependencyObject d)
        => (double)d.GetValue(AnimatedVerticalOffsetProperty);

    public static void SetAnimatedVerticalOffset(DependencyObject d, double value)
        => d.SetValue(AnimatedVerticalOffsetProperty, value);

    private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv && e.NewValue is double v)
            sv.ScrollToVerticalOffset(v);
    }

    /// <summary>
    /// Smoothly scroll <paramref name="scroller"/> to bring <paramref name="target"/>
    /// into view at the top of the viewport.
    /// </summary>
    public static void ScrollToElement(ScrollViewer scroller, FrameworkElement target, double durationMs = 380)
    {
        if (scroller == null || target == null) return;

        // Resolve the element's Y position relative to the scroll content.
        var content = scroller.Content as Visual ?? target;
        Point relative;
        try
        {
            relative = target.TransformToVisual((Visual)scroller.Content).Transform(new Point(0, 0));
        }
        catch
        {
            return;
        }

        var targetOffset = Math.Max(0, Math.Min(relative.Y, scroller.ScrollableHeight));

        var anim = new DoubleAnimation
        {
            From = scroller.VerticalOffset,
            To = targetOffset,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        scroller.BeginAnimation(AnimatedVerticalOffsetProperty, anim);
    }
}
