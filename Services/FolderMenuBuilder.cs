using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using TaskbarFolders.Models;
using TaskbarFolders.Native;
using Forms = System.Windows.Forms;

namespace TaskbarFolders.Services;

public static class FolderMenuBuilder
{
    private static readonly HashSet<string> ElevatableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".msi", ".msc", ".cpl", ".scr", ".ps1"
    };

    public static Forms.ContextMenuStrip Build(FolderEntry folder, AppConfig config)
    {
        var menu = CreateMenu();
        DisposeAfterClose(menu);
        Populate(menu, folder.Path, config, depth: 0, displayName: folder.Name);
        return menu;
    }

    public static void Populate(Forms.ToolStripDropDown menu, string path, AppConfig config, int depth, string? displayName = null)
    {
        menu.Items.Clear();

        if (!Directory.Exists(path))
        {
            menu.Items.Add(DisabledItem("(Folder not found)"));
            return;
        }

        var title = FolderTitle(path, displayName);
        var openItem = CreateCommandItem(title, () => OpenPath(path));
        openItem.Image = IconService.GetBitmap(path, isDirectory: true);
        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = config.ShowHiddenFiles
                    ? FileAttributes.System
                    : FileAttributes.Hidden | FileAttributes.System
            };

            var dirs = Directory.EnumerateDirectories(path, "*", options)
                .OrderBy(d => Path.GetFileName(d), StringComparer.CurrentCultureIgnoreCase)
                .Take(config.MaxItemsPerMenu)
                .ToList();

            var files = Directory.EnumerateFiles(path, "*", options)
                .OrderBy(f => Path.GetFileName(f), StringComparer.CurrentCultureIgnoreCase)
                .Take(config.MaxItemsPerMenu)
                .ToList();

            if (dirs.Count == 0 && files.Count == 0)
            {
                menu.Items.Add(DisabledItem("(Empty)"));
                return;
            }

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                var theme = UiTheme.Current();
                var item = new Forms.ToolStripMenuItem(name)
                {
                    Image = IconService.GetBitmap(dir, isDirectory: true),
                    DropDown = CreateMenu(),
                    ForeColor = theme.Text,
                    BackColor = Color.Transparent
                };

                item.DropDownItems.Add(DisabledItem("Loading…"));
                item.DropDownOpening += (_, _) =>
                {
                    if (item.DropDownItems.Count != 1 || item.DropDownItems[0].Text != "Loading…")
                        return;

                    if (depth >= 6)
                    {
                        item.DropDownItems.Clear();
                        var fallback = CreateCommandItem(name, () => OpenPath(dir));
                        fallback.Image = IconService.GetBitmap(dir, isDirectory: true);
                        item.DropDownItems.Add(fallback);
                    }
                    else
                    {
                        Populate(item.DropDown, dir, config, depth + 1, name);
                    }
                };

                menu.Items.Add(item);
            }

            foreach (var file in files)
            {
                var name = config.ShowFileExtensions
                    ? Path.GetFileName(file)
                    : Path.GetFileNameWithoutExtension(file);

                if (string.IsNullOrWhiteSpace(name))
                    name = Path.GetFileName(file);

                menu.Items.Add(CreateFileItem(name, file));
            }

            var truncated = Directory.EnumerateDirectories(path, "*", options).Skip(config.MaxItemsPerMenu).Any()
                || Directory.EnumerateFiles(path, "*", options).Skip(config.MaxItemsPerMenu).Any();

            if (truncated)
            {
                menu.Items.Add(new Forms.ToolStripSeparator());
                menu.Items.Add(DisabledItem($"Showing first {config.MaxItemsPerMenu} items…"));
                menu.Items.Add(CreateCommandItem("Open folder to see all", () => OpenPath(path)));
            }
        }
        catch (Exception ex)
        {
            menu.Items.Add(DisabledItem($"Error: {ex.Message}"));
        }
    }

    private static string FolderTitle(string path, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    internal static Forms.ContextMenuStrip CreateThemedStrip(bool showImageMargin = true, bool disposeAfterClose = true)
    {
        var menu = CreateMenu();
        menu.ShowImageMargin = showImageMargin;
        if (disposeAfterClose)
            DisposeAfterClose(menu);
        return menu;
    }

    private static FolderDropDown CreateMenu()
    {
        var theme = UiTheme.Current();
        return new FolderDropDown
        {
            ShowImageMargin = true,
            ShowCheckMargin = false,
            Renderer = new FolderMenuRenderer(theme),
            BackColor = theme.Back,
            ForeColor = theme.Text
        };
    }

    private sealed class FolderMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        private readonly UiTheme _theme;

        public FolderMenuRenderer(UiTheme theme)
            : base(new Forms.ProfessionalColorTable())
        {
            _theme = theme;
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            using var fill = new SolidBrush(_theme.Back);
            e.Graphics.FillRectangle(fill, e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e)
        {
            using var fill = new SolidBrush(_theme.Back);
            e.Graphics.FillRectangle(fill, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            var bounds = e.AffectedBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(_theme.Separator);
            e.Graphics.DrawLine(pen, 32, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            var pinned = (e.ToolStrip as FolderDropDown)?.PinnedItem;
            var hot = e.Item.Selected || ReferenceEquals(e.Item, pinned);
            if (!hot || !e.Item.Enabled)
                return;

            var bounds = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
            using var fill = new SolidBrush(_theme.Hot);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(bounds, 4);
            e.Graphics.FillPath(fill, path);
        }

        protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled != false ? _theme.Text : _theme.DisabledText;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _theme.Text : _theme.DisabledText;
            base.OnRenderItemText(e);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var d = Math.Max(1, radius * 2);
            if (bounds.Width < d || bounds.Height < d)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class FolderDropDown : Forms.ContextMenuStrip
    {
        internal bool KeepOpen;
        internal Forms.ToolStripItem? PinnedItem;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SetImmersiveDarkMode(Handle, !NativeMethods.IsSystemLightTheme());
        }

        protected override void OnClosing(Forms.ToolStripDropDownClosingEventArgs e)
        {
            if (KeepOpen && e.CloseReason is Forms.ToolStripDropDownCloseReason.AppFocusChange
                or Forms.ToolStripDropDownCloseReason.AppClicked)
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (KeepOpen)
                return;
            base.OnMouseLeave(e);
        }

        protected override void WndProc(ref Forms.Message m)
        {
            const int WmMouseLeave = 0x02A3;
            const int WmRButtonUp = 0x0205;

            if (KeepOpen && m.Msg == WmMouseLeave)
                return;

            if (m.Msg == WmRButtonUp)
            {
                var client = PointToClient(Forms.Cursor.Position);
                var hit = GetItemAt(client);
                if (hit?.Tag is string path)
                {
                    PinnedItem = hit;
                    Invalidate();
                    var screen = Forms.Cursor.Position;
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher != null)
                        dispatcher.BeginInvoke(() => ShowFileActionMenu(this, path, screen));
                    else
                        ShowFileActionMenu(this, path, screen);
                    return;
                }
            }

            base.WndProc(ref m);
        }
    }

    private static Forms.ToolStripMenuItem CreateFileItem(string header, string path)
    {
        var item = CreateCommandItem(header, () => OpenPath(path));
        item.Image = IconService.GetBitmap(path, isDirectory: false);
        item.Tag = path;
        item.BackColor = Color.Transparent;
        return item;
    }

    private static void ShowFileActionMenu(FolderDropDown source, string path, System.Drawing.Point screen)
    {
        SetKeepOpen(source, true);

        var actions = CreateMenu();
        actions.ShowImageMargin = false;
        actions.Items.Add(CreateCommandItem("Open", () =>
        {
            CloseFolderMenus(source);
            OpenPath(path);
        }));
        if (CanRunAsAdministrator(path))
        {
            actions.Items.Add(new Forms.ToolStripSeparator());
            actions.Items.Add(CreateCommandItem("Run as administrator", () =>
            {
                CloseFolderMenus(source);
                OpenPath(path, runAsAdmin: true);
            }));
        }

        actions.Closed += (_, _) => SetKeepOpen(source, false);
        DisposeAfterClose(actions);

        try
        {
            if (!source.IsDisposed && source.IsHandleCreated)
                actions.Show(source, source.PointToClient(screen));
            else
                actions.Show(screen);
        }
        catch (ObjectDisposedException)
        {
            actions.Show(screen);
        }

        try
        {
            source.PinnedItem?.Select();
            source.Invalidate();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void SetKeepOpen(FolderDropDown source, bool keep)
    {
        foreach (var menu in EnumerateChain(source))
        {
            if (menu.IsDisposed)
                continue;

            menu.KeepOpen = keep;
            menu.AutoClose = !keep;
            if (!keep)
                menu.PinnedItem = null;
            if (menu.IsHandleCreated)
                menu.Invalidate();
        }
    }

    private static void CloseFolderMenus(FolderDropDown source)
    {
        SetKeepOpen(source, false);
        try
        {
            var root = EnumerateChain(source).LastOrDefault();
            if (root is { IsDisposed: false, Visible: true })
                root.Close();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static List<FolderDropDown> EnumerateChain(FolderDropDown from)
    {
        var chain = new List<FolderDropDown>();
        Forms.ToolStripDropDown? current = from;
        while (current is FolderDropDown menu)
        {
            chain.Add(menu);
            current = menu.OwnerItem?.Owner as Forms.ToolStripDropDown;
        }

        return chain;
    }

    private static void DisposeAfterClose(Forms.ContextMenuStrip menu)
    {
        menu.Closed += (_, _) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
                return;

            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (!menu.IsDisposed)
                        menu.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    internal static Forms.ToolStripMenuItem CreateCommandItem(string header, Action action)
    {
        var theme = UiTheme.Current();
        var item = new Forms.ToolStripMenuItem(header)
        {
            ForeColor = theme.Text,
            BackColor = Color.Transparent
        };
        item.Click += (_, _) => action();
        return item;
    }

    private static Forms.ToolStripMenuItem DisabledItem(string header)
    {
        var theme = UiTheme.Current();
        return new Forms.ToolStripMenuItem(header)
        {
            Enabled = false,
            ForeColor = theme.DisabledText,
            BackColor = Color.Transparent
        };
    }

    private static bool CanRunAsAdministrator(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            if (!ShortcutResolver.TryGetLaunchInfo(path, out var launch))
                return false;

            return IsElevatableFile(launch.FileName);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsElevatableFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
            return false;

        return ElevatableExtensions.Contains(Path.GetExtension(path));
    }

    private static void OpenPath(string path, bool runAsAdmin = false)
    {
        try
        {
            if (runAsAdmin)
            {
                StartElevated(path);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled the UAC prompt.
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open:\n{path}\n\n{ex.Message}",
                "Taskbar Folders",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static void StartElevated(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "runas"
            });
            return;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return;
        }
        catch (Win32Exception)
        {
            // Some shortcuts reject runas on the .lnk; elevate the resolved target instead.
        }

        if (!ShortcutResolver.TryGetLaunchInfo(path, out var launch) || !IsElevatableFile(launch.FileName))
            throw new InvalidOperationException("This item is not an application.");

        var startInfo = new ProcessStartInfo
        {
            FileName = launch.FileName,
            UseShellExecute = true,
            Verb = "runas"
        };
        if (!string.IsNullOrEmpty(launch.Arguments))
            startInfo.Arguments = launch.Arguments;
        if (!string.IsNullOrWhiteSpace(launch.WorkingDirectory) && Directory.Exists(launch.WorkingDirectory))
            startInfo.WorkingDirectory = launch.WorkingDirectory;

        Process.Start(startInfo);
    }
}
