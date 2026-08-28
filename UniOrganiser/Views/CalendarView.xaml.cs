using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using UniOrganiser.ViewModels;

namespace UniOrganiser.Views;

public partial class CalendarView : UserControl
{
    // Month navigation is horizontal-swipe only - a vertical wheel over the grid is far too
    // easy to trigger by accident. Horizontal scrolling arrives as WM_MOUSEHWHEEL, which WPF
    // never surfaces as a routed event, so it needs a window message hook.
    private const int WmMouseHWheel = 0x020E;

    // One wheel notch worth of travel - how far a swipe must carry before it steps a month.
    private const int DeltaPerMonth = 120;

    // A gap this long in the delta stream means the gesture is over. Everything else - swipe
    // and inertia tail alike - arrives a few ms apart, so nothing mid-gesture can reach it.
    private const int GestureGapMs = 400;

    private const int NavigationLockMs = 1000;

    private HwndSource? _hwndSource;
    private long _lastDeltaTicks;
    private long _lastNavigationTicks;
    private bool _gestureConsumed;
    private int _accumulatedDelta;

    private readonly int _instanceId = SwipeLog.NextInstanceId();

    public CalendarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The ContentControl re-raises Loaded every time the view is navigated back to;
        // attaching twice would step two months per swipe.
        if (_hwndSource is not null)
        {
            SwipeLog.Write($"LOADED   instance={_instanceId} already-hooked, skipping. hooked={SwipeLog.HookedCount}");
            return;
        }

        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        if (_hwndSource is null)
        {
            SwipeLog.Write($"LOADED   instance={_instanceId} NO HwndSource. hooked={SwipeLog.HookedCount}");
            return;
        }

        _hwndSource.AddHook(OnWindowMessage);
        SwipeLog.Write($"ADDHOOK  instance={_instanceId} hwnd=0x{_hwndSource.Handle.ToInt64():X} hooked={SwipeLog.Hooked()}");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(OnWindowMessage);
            SwipeLog.Write($"RMVHOOK  instance={_instanceId} hooked={SwipeLog.Unhooked()}");
        }
        else
        {
            SwipeLog.Write($"UNLOADED instance={_instanceId} nothing hooked. hooked={SwipeLog.HookedCount}");
        }

        _hwndSource = null;
        SwipeLog.Flush();
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmMouseHWheel) return IntPtr.Zero;

        // Logged before the IsMouseOver filter so messages that get dropped are still visible.
        var delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
        var mouseOver = CalendarGridHost.IsMouseOver;
        SwipeLog.Message(_instanceId, delta, mouseOver);

        if (!mouseOver) return IntPtr.Zero;

        AccumulateAndNavigate(delta);
        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>
    /// Positive delta (a rightward swipe) moves forward a month, negative back. One gesture
    /// steps exactly one month: after stepping, the rest of the swipe is swallowed until the
    /// delta stream actually stops. A fixed cooldown cannot do this - the inertia tail has no
    /// fixed length and simply resumes once the window expires.
    /// </summary>
    private void AccumulateAndNavigate(int delta)
    {
        if (delta == 0) return;
        if (DataContext is not CalendarViewModel viewModel) return;

        var now = Environment.TickCount64;

        var sinceLastDelta = _lastDeltaTicks == 0
            ? long.MaxValue
            : now - _lastDeltaTicks;

        var sinceNavigation = _lastNavigationTicks == 0
            ? long.MaxValue
            : now - _lastNavigationTicks;

        _lastDeltaTicks = now;

        var latchBefore = _gestureConsumed;
        var accumBefore = _accumulatedDelta;

        // ------------------------------------------------------------
        // LOCKED
        // ------------------------------------------------------------
        if (_gestureConsumed)
        {
            // Never unlock while we're still inside the hard lock period.
            if (sinceNavigation < NavigationLockMs)
            {
                SwipeLog.Decision(
                    sinceLastDelta,
                    accumBefore,
                    _accumulatedDelta,
                    latchBefore,
                    true,
                    false,
                    $"locked ({sinceNavigation}ms since navigation)");

                return;
            }

            // Hard lock is over, but we ALSO require silence between
            // horizontal-wheel messages before considering the old
            // gesture finished.
            if (sinceLastDelta <= GestureGapMs)
            {
                SwipeLog.Decision(
                    sinceLastDelta,
                    accumBefore,
                    _accumulatedDelta,
                    latchBefore,
                    true,
                    false,
                    "waiting for quiet period");

                return;
            }

            // We finally believe the previous gesture is finished.
            //
            // Discard this message too. The NEXT message must start
            // the next gesture.
            _gestureConsumed = false;
            _accumulatedDelta = 0;

            SwipeLog.Decision(
                sinceLastDelta,
                accumBefore,
                0,
                latchBefore,
                false,
                true,
                "re-armed after lock + quiet period");

            return;
        }

        // ------------------------------------------------------------
        // ACCUMULATING A NEW GESTURE
        // ------------------------------------------------------------

        if (_accumulatedDelta != 0 &&
            Math.Sign(delta) != Math.Sign(_accumulatedDelta))
        {
            _accumulatedDelta = 0;
        }

        _accumulatedDelta += delta;

        if (Math.Abs(_accumulatedDelta) < DeltaPerMonth)
        {
            SwipeLog.Decision(
                sinceLastDelta,
                accumBefore,
                _accumulatedDelta,
                latchBefore,
                false,
                false,
                "accumulating");

            return;
        }

        // ------------------------------------------------------------
        // CHANGE MONTH
        // ------------------------------------------------------------

        var forward = _accumulatedDelta > 0;

        (forward
            ? viewModel.NextMonthCommand
            : viewModel.PreviousMonthCommand)
            .Execute(null);

        _lastNavigationTicks = now;
        _gestureConsumed = true;

        var accumAtStep = _accumulatedDelta;
        _accumulatedDelta = 0;

        SwipeLog.Decision(
            sinceLastDelta,
            accumBefore,
            accumAtStep,
            latchBefore,
            true,
            false,
            forward
                ? "STEP +1 month"
                : "STEP -1 month");
    }
}
    /// <summary>
    /// TEMPORARY diagnostic for the swipe-overshoot investigation. Buffers in memory because disk
    /// I/O inside the message hook would add latency to the very inter-delta gaps being measured.
    /// Remove this class, and every call to it, once the cause is identified.
    /// </summary>
    internal static class SwipeLog
{
    private static readonly string Path =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "uniorganiser-swipe.log");

    private const int MaxLines = 5000;

    private static readonly List<string> Lines = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Gate = new();

    private static int _instanceCounter;
    private static int _hooked;
    private static double _lastMessageMs;

    public static int HookedCount => _hooked;

    static SwipeLog()
    {
        // Start clean each run, since Flush appends and fires more than once per session.
        try
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
        catch (IOException)
        {
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
        Write($"=== session start, log at {Path} ===");
    }

    public static int NextInstanceId() => ++_instanceCounter;

    public static int Hooked() => ++_hooked;

    public static int Unhooked() => --_hooked;

    public static void Message(int instanceId, int delta, bool mouseOver)
    {
        var nowMs = Clock.Elapsed.TotalMilliseconds;
        var gapMs = _lastMessageMs == 0 ? 0 : nowMs - _lastMessageMs;
        _lastMessageMs = nowMs;

        Write($"WHEEL    t={nowMs,9:F1}ms gap={gapMs,7:F1}ms instance={instanceId} hooked={_hooked} " +
              $"mouseOver={mouseOver,-5} delta={delta,5}");
    }

    public static void Decision(long sinceLastDelta, int accumBefore, int accumAfter, bool latchBefore,
        bool latchAfter, bool cleared, string outcome)
    {
        Write($"  ->     sinceLastDelta(TickCount)={sinceLastDelta,5}ms cleared={cleared,-5} " +
              $"accum {accumBefore,5} -> {accumAfter,5}  latch {latchBefore,-5} -> {latchAfter,-5}  {outcome}");
    }

    public static void Write(string line)
    {
        lock (Gate)
        {
            if (Lines.Count < MaxLines) Lines.Add(line);
        }
    }

    public static void Flush()
    {
        lock (Gate)
        {
            if (Lines.Count == 0) return;

            var text = new StringBuilder();
            foreach (var line in Lines) text.AppendLine(line);

            try
            {
                File.AppendAllText(Path, text.ToString());
            }
            catch (IOException)
            {
                // Diagnostic only - never take the app down over a log write.
            }

            Lines.Clear();
        }
    }
}
