using System.Windows.Threading;
using TaskbarFolders.Native;

namespace TaskbarFolders.Services;

public sealed class TaskbarLocator
{
    private readonly DispatcherTimer _timer;
    private IntPtr _lastTray;
    private IntPtr _lastNotify;
    private int _lastTaskRight;
    private int _lastAppsRight;
    private int _lastTrayLeft;
    private int _lastNotifyLeft;
    private int _lastWidth;
    private bool? _lastLightTheme;

    public event Action? TaskbarChanged;
    public event Action? ThemeChanged;

    public TaskbarLocator()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => Refresh();
    }

    public void Start()
    {
        Refresh();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void Refresh()
    {
        var slots = NativeMethods.GetTaskbarSlots();
        var taskRight = slots.Taskband?.Right ?? slots.Rebar?.Right ?? 0;
        TaskbarLayout.TryGetAppClusterRight(slots.TrayHwnd, out var appsRight);
        TaskbarLayout.TryGetSystemTrayLeft(slots.TrayHwnd, out var trayLeft);
        var notifyLeft = slots.Notify?.Left ?? slots.TrayClient.Width;
        var width = slots.TrayClient.Width;
        var light = NativeMethods.IsSystemLightTheme();

        var changed = slots.TrayHwnd != _lastTray
            || slots.NotifyHwnd != _lastNotify
            || taskRight != _lastTaskRight
            || appsRight != _lastAppsRight
            || trayLeft != _lastTrayLeft
            || notifyLeft != _lastNotifyLeft
            || width != _lastWidth;

        var themeChanged = _lastLightTheme != light;

        _lastTray = slots.TrayHwnd;
        _lastNotify = slots.NotifyHwnd;
        _lastTaskRight = taskRight;
        _lastAppsRight = appsRight;
        _lastTrayLeft = trayLeft;
        _lastNotifyLeft = notifyLeft;
        _lastWidth = width;
        _lastLightTheme = light;

        // Explorer restacks the XAML island on top; keep re-asserting even when metrics are unchanged.
        if (changed || NativeMethods.HasXamlTaskbar(slots.TrayHwnd))
            TaskbarChanged?.Invoke();
        if (themeChanged)
            ThemeChanged?.Invoke();
    }
}
