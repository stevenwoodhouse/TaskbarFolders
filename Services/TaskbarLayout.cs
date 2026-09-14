using System.Windows.Automation;
using TaskbarFolders.Native;

namespace TaskbarFolders.Services;

internal static class TaskbarLayout
{
    public static int ReserveToolbarSlot(TaskbarSlots slots, int toolbarWidthPx, int alignment, int offsetPx)
    {
        if (!NativeMethods.HasXamlTaskbar(slots.TrayHwnd) || slots.TrayHwnd == IntPtr.Zero)
            return NativeMethods.ReserveToolbarSlot(slots, toolbarWidthPx, alignment, offsetPx);

        const int pad = 24;
        var notifyLeft = TryGetSystemTrayLeft(slots.TrayHwnd, out var trayLeft)
            ? trayLeft
            : slots.Notify?.Left ?? (int)(slots.TrayClient.Width * 0.78);
        var appsRight = TryGetAppClusterRight(slots.TrayHwnd, out var uiaRight) ? uiaRight : 55;

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
            x = stripLeft; // immediately before the system tray, not inside it

        x += offsetPx;
        return Math.Clamp(x, 4, Math.Max(4, notifyLeft - toolbarWidthPx - pad));
    }

    public static bool TryGetAppClusterRight(IntPtr tray, out int clientRight)
    {
        clientRight = 0;
        if (!TryGetRoot(tray, out var root, out var map))
            return false;

        try
        {
            var frame = root.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "TaskbarFrame"));
            if (frame is null)
                return false;

            var children = frame.FindAll(TreeScope.Children, Condition.TrueCondition);
            if (children.Count == 0)
                return false;

            double maxRight = 0;
            foreach (AutomationElement child in children)
            {
                var right = child.Current.BoundingRectangle.Right;
                if (right > maxRight)
                    maxRight = right;
            }

            if (maxRight <= 0)
                return false;

            clientRight = map(maxRight);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetSystemTrayLeft(IntPtr tray, out int clientLeft)
    {
        clientLeft = 0;
        if (!TryGetRoot(tray, out var root, out var map))
            return false;

        try
        {
            double minLeft = double.MaxValue;
            var bar = root.Current.BoundingRectangle;
            var cutoff = bar.X + Math.Max(bar.Width, 1) * 0.55;
            foreach (AutomationElement el in root.FindAll(TreeScope.Descendants, Condition.TrueCondition))
            {
                var id = el.Current.AutomationId ?? "";
                if (id is not ("SystemTrayIcon" or "NotifyItemIcon"))
                    continue;

                var rect = el.Current.BoundingRectangle;
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                var left = rect.X;
                // Clock/notify icons live on the right. Ignore anything over the app buttons.
                if (left >= cutoff && left < minLeft)
                    minLeft = left;
            }

            if (minLeft == double.MaxValue)
                return false;

            clientLeft = map(minLeft);
            NativeMethods.GetClientRect(tray, out var client);
            return clientLeft > client.Width / 2;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetRoot(
        IntPtr tray,
        out AutomationElement root,
        out Func<double, int> mapToClient)
    {
        root = null!;
        mapToClient = _ => 0;
        if (tray == IntPtr.Zero)
            return false;

        try
        {
            var found = AutomationElement.FromHandle(tray);
            if (found is null)
                return false;

            NativeMethods.GetClientRect(tray, out var client);
            var uia = found.Current.BoundingRectangle;
            var clientWidth = Math.Max(1, client.Width);
            var uiaWidth = uia.Width > 1 ? uia.Width : clientWidth;
            var origin = uia.X;
            var scale = clientWidth / uiaWidth;

            root = found;
            mapToClient = screenX => (int)Math.Round((screenX - origin) * scale);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
