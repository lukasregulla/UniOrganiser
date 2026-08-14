using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using UniOrganiser.ViewModels;

namespace UniOrganiser.Views;

public partial class CalendarView : UserControl
{
    // Horizontal trackpad scrolling arrives as WM_MOUSEHWHEEL, which WPF never surfaces
    // as a routed event, so it needs a window message hook.
    private const int WmMouseHWheel = 0x020E;

    private const int NavigationCooldownMs = 350;
    private const int DeltaThreshold = 60;

    private HwndSource? _hwndSource;
    private long _lastNavigationTicks;
    private int _accumulatedDelta;

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
        if (_hwndSource is not null) return;

        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(OnWindowMessage);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hwndSource?.RemoveHook(OnWindowMessage);
        _hwndSource = null;
    }

    private void CalendarGridHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        AccumulateAndNavigate(-e.Delta);
        e.Handled = true;
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmMouseHWheel || !CalendarGridHost.IsMouseOver) return IntPtr.Zero;

        var delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
        AccumulateAndNavigate(delta);
        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>
    /// Positive delta moves forward a month, negative back. Precision trackpads emit a
    /// burst of small deltas per swipe, so deltas are accumulated to a threshold and
    /// gated by a cooldown to keep one gesture to one month.
    /// </summary>
    private void AccumulateAndNavigate(int delta)
    {
        if (delta == 0) return;

        if (Environment.TickCount64 - _lastNavigationTicks < NavigationCooldownMs)
        {
            // Drop the momentum tail rather than queueing it up.
            _accumulatedDelta = 0;
            return;
        }

        // A reversal starts a fresh count instead of cancelling out leftover delta.
        if (Math.Sign(delta) != Math.Sign(_accumulatedDelta)) _accumulatedDelta = 0;

        _accumulatedDelta += delta;
        if (Math.Abs(_accumulatedDelta) < DeltaThreshold) return;

        if (DataContext is CalendarViewModel viewModel)
        {
            var command = _accumulatedDelta > 0 ? viewModel.NextMonthCommand : viewModel.PreviousMonthCommand;
            command.Execute(null);
        }

        _lastNavigationTicks = Environment.TickCount64;
        _accumulatedDelta = 0;
    }
}
