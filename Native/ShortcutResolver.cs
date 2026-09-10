using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarFolders.Native;

internal static class ShortcutResolver
{
    private const uint StgmRead = 0;
    private const uint SlrNoUi = 0x0001;
    private const uint SlrNoupdate = 0x0008;
    private const uint SlrNosearch = 0x0010;
    private const uint SlrNotrack = 0x0020;
    private const int MaxPathLong = 32768;

    public readonly record struct LaunchInfo(string FileName, string Arguments, string WorkingDirectory);

    public static bool TryGetLaunchInfo(string path, out LaunchInfo info)
    {
        info = default;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var fileName = path;
        var arguments = "";
        var workingDirectory = "";

        for (var hop = 0; hop < 3; hop++)
        {
            if (!fileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                break;

            if (!TryResolveLink(fileName, out var target, out var args, out var dir))
                return false;

            if (string.IsNullOrEmpty(arguments))
                arguments = args;
            if (string.IsNullOrEmpty(workingDirectory))
                workingDirectory = dir;

            fileName = target;
        }

        if (fileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(fileName))
            return false;

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            try
            {
                workingDirectory = Path.GetDirectoryName(fileName) ?? "";
            }
            catch
            {
                workingDirectory = "";
            }
        }

        info = new LaunchInfo(fileName, arguments, workingDirectory);
        return true;
    }

    private static bool TryResolveLink(string lnkPath, out string target, out string arguments, out string workingDirectory)
    {
        if (TryResolveWithWsh(lnkPath, out target, out arguments, out workingDirectory))
            return true;

        return TryResolveWithShellLink(lnkPath, out target, out arguments, out workingDirectory);
    }

    private static bool TryResolveWithWsh(string lnkPath, out string target, out string arguments, out string workingDirectory)
    {
        target = "";
        arguments = "";
        workingDirectory = "";
        object? shell = null;
        object? shortcut = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
                return false;

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
                return false;

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                shell,
                [lnkPath]);
            if (shortcut is null)
                return false;

            var shortcutType = shortcut.GetType();
            target = (shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string ?? "").Trim();
            arguments = shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string ?? "";
            workingDirectory = shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string ?? "";
            return !string.IsNullOrWhiteSpace(target);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (shortcut != null)
                Marshal.FinalReleaseComObject(shortcut);
            if (shell != null)
                Marshal.FinalReleaseComObject(shell);
        }
    }

    private static bool TryResolveWithShellLink(string lnkPath, out string target, out string arguments, out string workingDirectory)
    {
        target = "";
        arguments = "";
        workingDirectory = "";
        object? linkObject = null;

        try
        {
            linkObject = new ShellLink();
            var link = (IShellLinkW)linkObject;
            var persist = (IPersistFile)linkObject;
            persist.Load(lnkPath, StgmRead);

            try
            {
                // Fail quickly and silently if the target is missing.
                link.Resolve(IntPtr.Zero, SlrNoUi | (200u << 16) | SlrNoupdate | SlrNosearch | SlrNotrack);
            }
            catch
            {
                // Still try GetPath — some links are usable without Resolve.
            }

            var targetBuilder = new StringBuilder(MaxPathLong);
            link.GetPath(targetBuilder, targetBuilder.Capacity, IntPtr.Zero, 0);
            target = targetBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(target))
                return false;

            var argsBuilder = new StringBuilder(MaxPathLong);
            link.GetArguments(argsBuilder, argsBuilder.Capacity);
            arguments = argsBuilder.ToString();

            var dirBuilder = new StringBuilder(MaxPathLong);
            link.GetWorkingDirectory(dirBuilder, dirBuilder.Capacity);
            workingDirectory = dirBuilder.ToString();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (linkObject != null)
                Marshal.FinalReleaseComObject(linkObject);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
