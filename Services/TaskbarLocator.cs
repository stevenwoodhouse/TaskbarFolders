using System.Windows.Threading;
using TaskbarFolders.Native;

namespace TaskbarFolders.Services;

public sealed class TaskbarLocator
{
    private readonly DispatcherTimer _timer;
    private IntPtr _lastTray;
    private int _lastTaskRight;
    private int _lastNotifyLeft;
    private int _lastWidth;
    private bool? _lastLightTheme;

    public event Action? TaskbarChanged;
    public event Action? ThemeChanged;

    public TaskbarLocator()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
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
        var notifyLeft = slots.Notify?.Left ?? slots.TrayClient.Width;
        var width = slots.TrayClient.Width;
        var light = NativeMethods.IsSystemLightTheme();

        var changed = slots.TrayHwnd != _lastTray
            || taskRight != _lastTaskRight
            || notifyLeft != _lastNotifyLeft
            || width != _lastWidth;

        var themeChanged = _lastLightTheme != light;

        _lastTray = slots.TrayHwnd;
        _lastTaskRight = taskRight;
        _lastNotifyLeft = notifyLeft;
        _lastWidth = width;
        _lastLightTheme = light;

        if (changed)
            TaskbarChanged?.Invoke();
        if (themeChanged)
            ThemeChanged?.Invoke();
    }
}
