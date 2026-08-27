using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace UniOrganiser.Behaviours;

// WPF delivers a wheel notch as an instant jump, which reads as stuttering. This eases
// the offset across instead, and lets consecutive notches extend one glide rather than
// restarting from wherever the animation happens to have got to.
public static class SmoothScroll
{
    private const double DurationMs = 200;
    private const double PixelsPerLine = 16;
    // One full mouse-wheel notch. Doubles as the boundary between input kinds: anything
    // smaller is a precision trackpad emitting a continuous stream.
    private const double DeltaPerNotch = 120;

    // How long a target stays trustworthy once deltas stop arriving.
    private const int ResyncGapMs = 150;

    // A task row is roughly 85px tall, so an unmultiplied notch would move half a row and
    // then spend the ease-out tail crawling to a halt - which reads as the list refusing
    // to scroll rather than as a glide.
    private const double NotchMultiplier = 3;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SmoothScroll),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    // ScrollViewer.VerticalOffset is read-only, so the animation drives this instead and
    // each frame is pushed through ScrollToVerticalOffset.
    private static readonly DependencyProperty AnimatedOffsetProperty =
        DependencyProperty.RegisterAttached("AnimatedOffset", typeof(double), typeof(SmoothScroll),
            new PropertyMetadata(0d, OnAnimatedOffsetChanged));

    // Where the scroll is heading. NaN means idle, in which case the next delta starts from
    // the live offset - that is what lets a scrollbar drag hand back cleanly.
    private static readonly DependencyProperty TargetOffsetProperty =
        DependencyProperty.RegisterAttached("TargetOffset", typeof(double), typeof(SmoothScroll),
            new PropertyMetadata(double.NaN));

    // The direct path has no Completed callback to reset TargetOffset, so staleness is
    // judged on time instead.
    private static readonly DependencyProperty LastDeltaTicksProperty =
        DependencyProperty.RegisterAttached("LastDeltaTicks", typeof(long), typeof(SmoothScroll),
            new PropertyMetadata(0L));

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ScrollViewer scrollViewer) return;

        scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        if (e.NewValue is true) scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnAnimatedOffsetChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is ScrollViewer scrollViewer) scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;

        var scrollViewer = (ScrollViewer)sender;
        if (scrollViewer.ScrollableHeight <= 0) return;

        var now = Environment.TickCount64;
        var stale = now - (long)scrollViewer.GetValue(LastDeltaTicksProperty) > ResyncGapMs;
        scrollViewer.SetValue(LastDeltaTicksProperty, now);

        // Mid-gesture, chain off the running target so nothing is lost to ScrollToVerticalOffset
        // not landing until the next layout pass. Once deltas stop, re-read where we really are.
        var inFlight = (double)scrollViewer.GetValue(TargetOffsetProperty);
        var from = double.IsNaN(inFlight) || stale ? scrollViewer.VerticalOffset : inFlight;
        var step = e.Delta / DeltaPerNotch * StepPixels(scrollViewer);
        var target = Math.Clamp(from - step, 0, scrollViewer.ScrollableHeight);

        e.Handled = true;
        if (target == from) return;

        if (Math.Abs(e.Delta) < DeltaPerNotch) Apply(scrollViewer, target);
        else Glide(scrollViewer, target);
    }

    // Trackpad deltas arrive pre-smoothed at input frequency. Easing them means every delta
    // cancels the animation the one before it started, before that has moved anything, so the
    // list looks frozen until the finger lifts. Apply directly instead, and drop any glide
    // still in flight: a finger on the trackpad outranks a notch from a moment ago.
    private static void Apply(ScrollViewer scrollViewer, double target)
    {
        scrollViewer.SetValue(TargetOffsetProperty, target);
        scrollViewer.SetValue(AnimatedOffsetProperty, target);
        scrollViewer.BeginAnimation(AnimatedOffsetProperty, null);
    }

    // Scales the Windows wheel setting rather than replacing it. A negative WheelScrollLines
    // is the "one screen at a time" setting, which is already a large step and is left alone.
    private static double StepPixels(ScrollViewer scrollViewer)
    {
        var lines = SystemParameters.WheelScrollLines;
        return lines < 0 ? scrollViewer.ViewportHeight : lines * PixelsPerLine * NotchMultiplier;
    }

    private static void Glide(ScrollViewer scrollViewer, double target)
    {
        scrollViewer.SetValue(TargetOffsetProperty, target);

        // Clearing an animation reverts the property to its base value, so the base has to
        // be written first or the offset snaps backwards for a frame. Same reason it is
        // done again on completion.
        scrollViewer.SetValue(AnimatedOffsetProperty, scrollViewer.VerticalOffset);
        scrollViewer.BeginAnimation(AnimatedOffsetProperty, null);

        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(DurationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        animation.Completed += (_, _) =>
        {
            // A notch part-way through supersedes this animation; ignore its completion
            // rather than dragging the offset back to a stale target.
            if (!Equals(scrollViewer.GetValue(TargetOffsetProperty), target)) return;

            scrollViewer.SetValue(AnimatedOffsetProperty, target);
            scrollViewer.BeginAnimation(AnimatedOffsetProperty, null);
            scrollViewer.SetValue(TargetOffsetProperty, double.NaN);
        };

        scrollViewer.BeginAnimation(AnimatedOffsetProperty, animation);
    }
}
