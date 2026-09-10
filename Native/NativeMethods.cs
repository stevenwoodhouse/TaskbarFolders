using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace TaskbarFolders.Native;

internal static class NativeMethods
{
    public const int GwlStyle = -16;
    public const int GwlExstyle = -20;

    public const int WsChild = 0x40000000;
    public const int WsPopup = unchecked((int)0x80000000);
    public const int WsVisible = 0x10000000;
    public const int WsClipchildren = 0x02000000;
    public const int WsClipsiblings = 0x04000000;
    public const int WsBorder = 0x00800000;
    public const int WsCaption = 0x00C00000;
    public const int WsThickframe = 0x00040000;
    public const int WsDlgframe = 0x00400000;
    public const int WsSysmenu = 0x00080000;

    public const int WsExToolwindow = 0x00000080;
    public const int WsExNoactivate = 0x08000000;
    public const int WsExTopmost = 0x00000008;
    public const int WsExLayered = 0x00080000;

    public const uint SwpNosize = 0x0001;
    public const uint SwpNomove = 0x0002;
    public const uint SwpNozorder = 0x0004;
    public const uint SwpNoactivate = 0x0010;
    public const uint SwpShowwindow = 0x0040;
    public const uint SwpFramechanged = 0x0020;

    public const uint RbDeleteBand = 0x0402;
    public const uint RbGetBandCount = 0x040C;
    public const uint RbGetBandInfoW = 0x041C;
    public const uint RbbimId = 0x00000100;
    public const int BandId = 0x54424631;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RectNative lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hWnd, out RectNative lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref PointNative lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, ref RebarBandInfo lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref ShFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    public const uint ShgfiIcon = 0x000000100;
    public const uint ShgfiSmallicon = 0x000000001;
    public const uint ShgfiUsefileattributes = 0x000000010;
    public const uint FileAttributeDirectory = 0x00000010;
    public const uint FileAttributeNormal = 0x00000080;

    [StructLayout(LayoutKind.Sequential)]
    public struct RectNative
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PointNative
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RectInterop
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RebarBandInfo
    {
        public uint cbSize;
        public uint fMask;
        public uint fStyle;
        public uint clrFore;
        public uint clrBack;
        public IntPtr lpText;
        public uint cch;
        public int iImage;
        public IntPtr hwndChild;
        public uint cxMinChild;
        public uint cyMinChild;
        public uint cx;
        public IntPtr hbmBack;
        public uint wID;
        public uint cyChild;
        public uint cyMaxChild;
        public uint cyIntegral;
        public uint cxIdeal;
        public IntPtr lParam;
        public uint cxHeader;
        public RectInterop rcChevronLocation;
        public uint uChevronState;
    }

    public static IntPtr GetTaskbarHwnd() => FindWindow("Shell_TrayWnd", null);

    public static IntPtr FindChildByClass(IntPtr parent, string className)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(parent, (h, _) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, sb.Capacity);
            if (string.Equals(sb.ToString(), className, StringComparison.Ordinal))
            {
                found = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void PrepareChildWindow(Window window)
    {
        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        var hwnd = helper.Handle;

        var style = GetWindowLong(hwnd, GwlStyle);
        style |= WsChild | WsClipchildren | WsClipsiblings | WsVisible;
        style &= ~(WsPopup | WsCaption | WsThickframe | WsBorder | WsDlgframe | WsSysmenu);
        SetWindowLong(hwnd, GwlStyle, style);

        var ex = GetWindowLong(hwnd, GwlExstyle);
        ex |= WsExToolwindow | WsExNoactivate | WsExLayered;
        ex &= ~WsExTopmost;
        SetWindowLong(hwnd, GwlExstyle, ex);

        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SwpNomove | SwpNosize | SwpNozorder | SwpNoactivate | SwpFramechanged);
    }

    public static void CleanupStaleRebarBands()
    {
        var tray = GetTaskbarHwnd();
        if (tray == IntPtr.Zero) return;
        var rebar = FindWindowEx(tray, IntPtr.Zero, "ReBarWindow32", null);
        if (rebar == IntPtr.Zero) rebar = FindChildByClass(tray, "ReBarWindow32");
        if (rebar == IntPtr.Zero) return;

        for (var i = (int)SendMessage(rebar, RbGetBandCount, IntPtr.Zero, IntPtr.Zero) - 1; i >= 0; i--)
        {
            var info = new RebarBandInfo
            {
                cbSize = (uint)Marshal.SizeOf<RebarBandInfo>(),
                fMask = RbbimId
            };
            SendMessage(rebar, RbGetBandInfoW, new IntPtr(i), ref info);
            if (info.wID == BandId)
                SendMessage(rebar, RbDeleteBand, new IntPtr(i), IntPtr.Zero);
        }
    }

    public static TaskbarSlots GetTaskbarSlots()
    {
        var tray = GetTaskbarHwnd();
        if (tray == IntPtr.Zero)
            return new TaskbarSlots(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, default, null, null, null, TaskbarEdge.Bottom);

        GetWindowRect(tray, out var trayScreen);
        GetClientRect(tray, out var trayClient);

        var rebarHwnd = FindWindowEx(tray, IntPtr.Zero, "ReBarWindow32", null);
        if (rebarHwnd == IntPtr.Zero)
            rebarHwnd = FindChildByClass(tray, "ReBarWindow32");

        var taskHwnd = FindWindowEx(tray, IntPtr.Zero, "MSTaskSwWClass", null);
        if (taskHwnd == IntPtr.Zero && rebarHwnd != IntPtr.Zero)
            taskHwnd = FindWindowEx(rebarHwnd, IntPtr.Zero, "MSTaskSwWClass", null);
        if (taskHwnd == IntPtr.Zero)
            taskHwnd = FindChildByClass(tray, "MSTaskSwWClass");

        var notifyHwnd = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);

        RectNative? rebar = rebarHwnd != IntPtr.Zero ? ToClientRect(tray, rebarHwnd) : null;
        RectNative? taskband = taskHwnd != IntPtr.Zero ? ToClientRect(tray, taskHwnd) : null;
        RectNative? notify = notifyHwnd != IntPtr.Zero ? ToClientRect(tray, notifyHwnd) : null;

        var edge = TaskbarEdge.Bottom;
        if (trayScreen.Top <= 2 && trayScreen.Height < trayScreen.Width)
            edge = TaskbarEdge.Top;
        else if (trayScreen.Left <= 2 && trayScreen.Width < trayScreen.Height)
            edge = TaskbarEdge.Left;
        else if (trayScreen.Width < trayScreen.Height)
            edge = TaskbarEdge.Right;

        return new TaskbarSlots(tray, rebarHwnd, taskHwnd, notifyHwnd, trayClient, rebar, taskband, notify, edge);
    }

    private static RectNative ToClientRect(IntPtr parent, IntPtr child)
    {
        GetWindowRect(child, out var screen);
        var tl = new PointNative { X = screen.Left, Y = screen.Top };
        var br = new PointNative { X = screen.Right, Y = screen.Bottom };
        ScreenToClient(parent, ref tl);
        ScreenToClient(parent, ref br);
        return new RectNative { Left = tl.X, Top = tl.Y, Right = br.X, Bottom = br.Y };
    }

    public static bool TryEmbedInTaskbar(Window window)
    {
        var tray = GetTaskbarHwnd();
        if (tray == IntPtr.Zero || !IsWindow(tray))
            return false;

        PrepareChildWindow(window);
        var hwnd = new WindowInteropHelper(window).Handle;
        SetParent(hwnd, tray);
        return GetParent(hwnd) == tray;
    }

    public static void PositionChild(IntPtr hwnd, int x, int y, int width, int height) =>
        SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SwpNoactivate | SwpShowwindow | SwpNozorder);

    /// <summary>
    /// Carve a dedicated strip for the toolbar (like a Win10 taskbar toolbar) and
    /// give the remaining space to the task buttons. Returns the toolbar X in tray client coords.
    /// </summary>
    public static int ReserveToolbarSlot(TaskbarSlots slots, int toolbarWidthPx, int alignment, int offsetPx)
    {
        if (slots.TrayHwnd == IntPtr.Zero)
            return 8;

        var notifyLeft = slots.Notify?.Left ?? slots.TrayClient.Width - 8;
        var taskLeft = slots.Taskband?.Left ?? slots.Rebar?.Left ?? 55;
        var height = Math.Max(slots.TrayClient.Height, 40);

        const int pad = 8;
        var minTaskWidth = 160;

        // Dedicated strip immediately left of the system tray (classic toolbar side).
        var stripRight = notifyLeft - pad;
        var stripLeft = stripRight - toolbarWidthPx;

        // Task buttons keep everything to the left of that strip.
        var desiredTaskWidth = Math.Max(minTaskWidth, stripLeft - pad - taskLeft);
        if (slots.TaskHwnd != IntPtr.Zero || slots.RebarHwnd != IntPtr.Zero)
            ResizeTaskband(slots, desiredTaskWidth, height);

        // Re-read after resize so placement matches reality.
        slots = GetTaskbarSlots();
        notifyLeft = slots.Notify?.Left ?? notifyLeft;
        var taskRight = slots.Taskband?.Right ?? slots.Rebar?.Right ?? (taskLeft + desiredTaskWidth);
        stripRight = notifyLeft - pad;
        stripLeft = Math.Max(taskRight + pad, stripRight - toolbarWidthPx);

        var freeLeft = taskRight + pad;
        var freeRight = notifyLeft - pad;

        int x;
        if (alignment == 0)
            x = freeLeft; // after task buttons
        else if (alignment == 1)
            x = freeLeft + Math.Max(0, (freeRight - freeLeft - toolbarWidthPx) / 2);
        else
            x = stripLeft; // before system tray

        x += offsetPx;
        return Math.Clamp(x, 4, Math.Max(4, slots.TrayClient.Width - toolbarWidthPx - 4));
    }

    public static void ResizeTaskband(TaskbarSlots slots, int widthPx, int heightPx)
    {
        var left = slots.Rebar?.Left ?? slots.Taskband?.Left ?? 55;
        var top = slots.Rebar?.Top ?? slots.Taskband?.Top ?? 0;

        if (slots.RebarHwnd != IntPtr.Zero && IsWindow(slots.RebarHwnd))
        {
            // ReBar is a child of the tray — use client coords of tray.
            SetWindowPos(slots.RebarHwnd, IntPtr.Zero, left, top, widthPx, heightPx,
                SwpNoactivate | SwpNozorder | SwpShowwindow);
        }

        if (slots.TaskHwnd != IntPtr.Zero && IsWindow(slots.TaskHwnd))
        {
            // MSTaskSw is usually a child of ReBar — position relative to rebar parent.
            var parent = GetParent(slots.TaskHwnd);
            if (parent == slots.RebarHwnd)
                SetWindowPos(slots.TaskHwnd, IntPtr.Zero, 0, 0, widthPx, heightPx,
                    SwpNoactivate | SwpNozorder | SwpShowwindow);
            else
                SetWindowPos(slots.TaskHwnd, IntPtr.Zero, left, top, widthPx, heightPx,
                    SwpNoactivate | SwpNozorder | SwpShowwindow);
        }
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public const int DwmwaUseImmersiveDarkMode = 20;
    public const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero)
            return;

        var value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
    }

    public static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("SystemUsesLightTheme");
            return value != null && Convert.ToInt32(value) == 1;
        }
        catch
        {
            return false;
        }
    }

    public static double GetDpiScale()
    {
        try
        {
            using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            return g.DpiX / 96.0;
        }
        catch
        {
            return 1.0;
        }
    }
}

internal enum TaskbarEdge { Bottom, Top, Left, Right }

internal readonly record struct TaskbarSlots(
    IntPtr TrayHwnd,
    IntPtr RebarHwnd,
    IntPtr TaskHwnd,
    IntPtr NotifyHwnd,
    NativeMethods.RectNative TrayClient,
    NativeMethods.RectNative? Rebar,
    NativeMethods.RectNative? Taskband,
    NativeMethods.RectNative? Notify,
    TaskbarEdge Edge);
