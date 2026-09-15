using System.Windows.Threading;
using TaskbarFolders.Native;

namespace TaskbarFolders.Services;

public sealed class TaskbarLocator
{
    private readonly DispatcherTimer _timer;
    private IntPtr _lastTray;
    private IntPtr _lastNotify;
    private int _lastNotifyLeft;
    private int _lastWidth;
    private bool? _lastLightTheme;
    private long _lastUiaMs;
    private bool _skipUiaOnce = true;

    public event Action? TaskbarChanged;
    public event Action? KeepOnTop;
    public event Action? ThemeChanged;

    public TaskbarLocator()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => Refresh();
    }

    public void Start()
    {
        _skipUiaOnce = true;
        Refresh();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void Refresh()
    {
        var slots = NativeMethods.GetTaskbarSlots();
        var notifyLeft = slots.Notify?.Left ?? slots.TrayClient.Width;
        var width = slots.TrayClient.Width;
        var light = NativeMethods.IsSystemLightTheme();
        var now = Environment.TickCount64;

        var win32Changed = slots.TrayHwnd != _lastTray
            || slots.NotifyHwnd != _lastNotify
            || notifyLeft != _lastNotifyLeft
            || width != _lastWidth;

        var themeChanged = _lastLightTheme != light;

        if (win32Changed)
        {
            TaskbarLayout.Invalidate();
            _lastUiaMs = 0;
        }

        _lastTray = slots.TrayHwnd;
        _lastNotify = slots.NotifyHwnd;
        _lastNotifyLeft = notifyLeft;
        _lastWidth = width;
        _lastLightTheme = light;

        var runUia = !_skipUiaOnce && slots.TrayHwnd != IntPtr.Zero
            && (win32Changed || now - _lastUiaMs >= 1000);
        _skipUiaOnce = false;

        if (runUia)
        {
            TaskbarLayout.RefreshUia(slots.TrayHwnd);
            _lastUiaMs = now;
            TaskbarChanged?.Invoke();
        }
        else if (win32Changed)
        {
            TaskbarChanged?.Invoke();
        }
        else if (NativeMethods.HasXamlTaskbar(slots.TrayHwnd))
        {
            KeepOnTop?.Invoke();
        }

        if (themeChanged)
            ThemeChanged?.Invoke();
    }
}
