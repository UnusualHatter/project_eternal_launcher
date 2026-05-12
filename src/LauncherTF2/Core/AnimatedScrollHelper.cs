using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LauncherTF2.Core;

/// <summary>
/// Smooth scroll-to-element for any <see cref="ScrollViewer"/>. Drives the
/// offset from <see cref="CompositionTarget.Rendering"/> — that hook fires
/// once per WPF render frame, in sync with the compositor, so motion is
/// frame-locked and immune to dispatcher contention. We compute progress from
/// real wall-clock time (Stopwatch) instead of accumulating a fixed step, so
/// missed frames do not slow the animation down.
///
/// Public surface (<see cref="ScrollToElement"/>, <see cref="IsAnimating"/>,
/// <see cref="StopAnimation"/>) is preserved so call sites do not change.
/// </summary>
public static class AnimatedScrollHelper
{
    private sealed class State
    {
        public double From;
        public double To;
        public double DurationMs;
        public Stopwatch Watch = Stopwatch.StartNew();
        public Action? OnCompleted;
    }

    private static readonly Dictionary<ScrollViewer, State> _running = new();
    private static readonly HashSet<ScrollViewer> _draining = new();
    private static bool _hooked;

    /// <summary>
    /// True while a scroll animation is mid-flight on this scroller, OR while
    /// the post-animation drain window is open. Callers (notably the Settings
    /// view's <c>ContentScroller_ScrollChanged</c>) gate side-effects on this
    /// to avoid reacting to the storm of <c>ScrollChanged</c> events the
    /// animation itself generates.
    /// </summary>
    public static bool IsAnimating(ScrollViewer sv) => _running.ContainsKey(sv) || _draining.Contains(sv);

    /// <summary>Cancels every in-flight animation. Currently only used by tests / safety nets.</summary>
    public static void StopAnimation()
    {
        _running.Clear();
        UnhookIfIdle();
    }

    public static void ScrollToElement(
        ScrollViewer scroller,
        FrameworkElement target,
        double durationMs = 380,
        Action? onCompleted = null)
    {
        if (scroller == null || target == null)
        {
            onCompleted?.Invoke();
            return;
        }

        try
        {
            if (scroller.Content is not Visual content)
            {
                onCompleted?.Invoke();
                return;
            }

            // Force a layout pass before sampling the target's position. Without
            // this, a freshly rebuilt schema (Reset) or a category that just got
            // its DataTemplate inflated can report a stale/zero Y, which then
            // either teleports to the top or undershoots wildly.
            scroller.UpdateLayout();

            var pos = target.TransformToVisual(content).Transform(new Point(0, 0));
            double from = scroller.VerticalOffset;
            double to = Math.Max(0, Math.Min(pos.Y, scroller.ScrollableHeight));

            if (Math.Abs(from - to) < 1.0)
            {
                onCompleted?.Invoke();
                return;
            }

            // The latest request wins — replacing the State entry effectively
            // cancels the previous animation on this scroller. The next render
            // tick will pick up the new From/To from where we currently are.
            _running[scroller] = new State
            {
                From = from,
                To = to,
                DurationMs = durationMs,
                OnCompleted = onCompleted
            };

            Hook();
        }
        catch
        {
            onCompleted?.Invoke();
        }
    }

    private static void Hook()
    {
        if (_hooked) return;
        CompositionTarget.Rendering += OnRender;
        _hooked = true;
    }

    private static void UnhookIfIdle()
    {
        if (_hooked && _running.Count == 0)
        {
            CompositionTarget.Rendering -= OnRender;
            _hooked = false;
        }
    }

    private static void OnRender(object? sender, EventArgs e)
    {
        if (_running.Count == 0)
        {
            UnhookIfIdle();
            return;
        }

        // Snapshot the running set so a Completed callback that re-enters
        // ScrollToElement can mutate _running without invalidating us.
        foreach (var (sv, state) in _running.ToArray())
        {
            double t = Math.Clamp(state.Watch.Elapsed.TotalMilliseconds / state.DurationMs, 0.0, 1.0);
            // Ease-out cubic — fast start, gentle settle.
            double eased = 1.0 - Math.Pow(1.0 - t, 3.0);
            double offset = state.From + (state.To - state.From) * eased;

            sv.ScrollToVerticalOffset(offset);

            if (t >= 1.0)
            {
                // Pin the destination, then move to the drain phase. Guard
                // against a re-entrant ScrollToElement having already replaced
                // the entry under us with a fresh animation.
                if (_running.TryGetValue(sv, out var current) && ReferenceEquals(current, state))
                {
                    sv.ScrollToVerticalOffset(state.To);
                    _running.Remove(sv);
                    BeginDrain(sv, state.OnCompleted);
                }
            }
        }

        UnhookIfIdle();
    }

    /// <summary>
    /// Hold IsAnimating=true for two extra dispatcher frames after the
    /// animation finishes. WPF posts a few ScrollChanged events on its own
    /// after the final ScrollToVerticalOffset settles; without this drain the
    /// sidebar's active-section recompute would fire once with the destination
    /// offset but stale anchor positions and briefly mis-highlight.
    /// </summary>
    private static void BeginDrain(ScrollViewer sv, Action? onCompleted)
    {
        _draining.Add(sv);
        sv.Dispatcher.InvokeAsync(() =>
        {
            sv.Dispatcher.InvokeAsync(() =>
            {
                _draining.Remove(sv);
                onCompleted?.Invoke();
            }, DispatcherPriority.Background);
        }, DispatcherPriority.Background);
    }
}
