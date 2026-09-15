using System.Runtime.InteropServices;
using TaskbarFolders.Native;
using Forms = System.Windows.Forms;

namespace TaskbarFolders.Services;

/// <summary>
/// Survives Explorer restarts (unlike a child of Shell_TrayWnd) so we can re-embed
/// after a theme change recreates the taskbar.
/// </summary>
internal sealed class ShellMessageWindow : Forms.NativeWindow, IDisposable
{
    private const int WmSettingChange = 0x001A;
    private readonly uint _taskbarCreated = NativeMethods.WmTaskbarCreated;

    public event Action? TaskbarCreated;
    public event Action? ThemeChanged;

    public ShellMessageWindow()
    {
        var cp = new Forms.CreateParams
        {
            Caption = "TaskbarFolders.ShellMessages",
            Parent = NativeMethods.HwndMessage
        };
        CreateHandle(cp);
    }

    protected override void WndProc(ref Forms.Message m)
    {
        if (m.Msg == (int)_taskbarCreated)
        {
            TaskbarCreated?.Invoke();
        }
        else if (m.Msg == WmSettingChange && m.LParam != IntPtr.Zero)
        {
            var name = Marshal.PtrToStringAuto(m.LParam);
            if (string.Equals(name, "ImmersiveColorSet", StringComparison.Ordinal))
                ThemeChanged?.Invoke();
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
            DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
