using System.Windows.Automation;
using TaskbarFolders.Native;

namespace TaskbarFolders.Services;

internal static class TaskbarLayout
{
    private static IntPtr _cachedTray;
    private static long _cachedAtMs;
    private static int _cachedTrayLeft;
    private static int _cachedAppsRight;
    private static bool _haveTrayLeft;
    private static bool _haveAppsRight;

    public static void Invalidate()
    {
        _cachedTray = IntPtr.Zero;
        _haveTrayLeft = false;
        _haveAppsRight = false;
    }

    public static int GetSystemTrayLeft(TaskbarSlots slots)
    {
        if (_haveTrayLeft && slots.TrayHwnd == _cachedTray)
            return _cachedTrayLeft;
        return slots.Notify?.Left ?? (int)(slots.TrayClient.Width * 0.78);
    }

    public static int GetAppClusterRight(TaskbarSlots slots)
    {
        if (_haveAppsRight && slots.TrayHwnd == _cachedTray)
            return _cachedAppsRight;
        return 55;
    }

    public static int ReserveToolbarSlot(TaskbarSlots slots, int toolbarWidthPx, int alignment, int offsetPx)
    {
        if (!NativeMethods.HasXamlTaskbar(slots.TrayHwnd) || slots.TrayHwnd == IntPtr.Zero)
            return NativeMethods.ReserveToolbarSlot(slots, toolbarWidthPx, alignment, offsetPx);

        const int pad = 24;
        var notifyLeft = GetSystemTrayLeft(slots);
        var appsRight = GetAppClusterRight(slots);

        var stripRight = notifyLeft - pad;
        var stripLeft = stripRight - toolbarWidthPx;
        var freeLeft = appsRight + pad;
        var freeRight = notifyLeft - pad;

        int x;
        if (alignment == 0)
            x = freeLeft;
        else if (alignment == 1)
            x = freeLeft + Math.Max(0, (freeRight - freeLeft - toolbarWidthPx) / 2);
        else
            x = stripLeft;

        x += offsetPx;
        return Math.Clamp(x, 4, Math.Max(4, notifyLeft - toolbarWidthPx - pad));
    }

    /// <summary>
    /// Slow path: UI Automation. Call off the first paint, and only when layout may have changed.
    /// </summary>
    public static void RefreshUia(IntPtr tray)
    {
        if (tray == IntPtr.Zero)
            return;
        if (tray == _cachedTray && Environment.TickCount64 - _cachedAtMs < 400)
            return;

        try
        {
            var root = AutomationElement.FromHandle(tray);
            if (root is null)
                return;

            NativeMethods.GetClientRect(tray, out var client);
            var uia = root.Current.BoundingRectangle;
            var clientWidth = Math.Max(1, client.Width);
            var uiaWidth = uia.Width > 1 ? uia.Width : clientWidth;
            var origin = uia.X;
            var scale = clientWidth / uiaWidth;
            int Map(double screenX) => (int)Math.Round((screenX - origin) * scale);

            var frame = root.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "TaskbarFrame"));
            if (frame != null)
            {
                double maxRight = 0;
                foreach (AutomationElement child in frame.FindAll(TreeScope.Children, Condition.TrueCondition))
                {
                    var right = child.Current.BoundingRectangle.Right;
                    if (right > maxRight)
                        maxRight = right;
                }

                if (maxRight > 0)
                {
                    _cachedAppsRight = Map(maxRight);
                    _haveAppsRight = true;
                }
            }

            var trayIcons = root.FindAll(
                TreeScope.Descendants,
                new OrCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon"),
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "NotifyItemIcon")));

            double minLeft = double.MaxValue;
            var cutoff = origin + Math.Max(uiaWidth, 1) * 0.55;
            foreach (AutomationElement el in trayIcons)
            {
                var rect = el.Current.BoundingRectangle;
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;
                if (rect.X >= cutoff && rect.X < minLeft)
                    minLeft = rect.X;
            }

            if (minLeft != double.MaxValue)
            {
                var mapped = Map(minLeft);
                if (mapped > client.Width / 2)
                {
                    _cachedTrayLeft = mapped;
                    _haveTrayLeft = true;
                }
            }

            _cachedTray = tray;
            _cachedAtMs = Environment.TickCount64;
        }
        catch
        {
            // Explorer may be mid-restart; keep last cache.
        }
    }
}
