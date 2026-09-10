using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskbarFolders.Native;

namespace TaskbarFolders.Services;

public static class IconService
{
    public static ImageSource? GetIcon(string path, bool isDirectory)
    {
        try
        {
            var info = new NativeMethods.ShFileInfo();
            uint flags = NativeMethods.ShgfiIcon | NativeMethods.ShgfiSmallicon;
            uint attrs = 0;

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                flags |= NativeMethods.ShgfiUsefileattributes;
                attrs = isDirectory
                    ? NativeMethods.FileAttributeDirectory
                    : NativeMethods.FileAttributeNormal;
            }

            NativeMethods.SHGetFileInfo(path, attrs, ref info, (uint)Marshal.SizeOf<NativeMethods.ShFileInfo>(), flags);
            if (info.hIcon == IntPtr.Zero)
                return null;

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(16, 16));
                source.Freeze();
                return source;
            }
            finally
            {
                NativeMethods.DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    public static Icon? GetAppIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                return Icon.ExtractAssociatedIcon(exe);
        }
        catch
        {
            // ignore
        }

        return SystemIcons.Application;
    }
}
