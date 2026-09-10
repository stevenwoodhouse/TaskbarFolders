using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TaskbarFolders.Models;

namespace TaskbarFolders.Services;

public static class FolderMenuBuilder
{
    public static ContextMenu Build(FolderEntry folder, AppConfig config)
    {
        var menu = CreateMenu();
        Populate(menu.Items, folder.Path, config, depth: 0);
        return menu;
    }

    public static void Populate(ItemCollection items, string path, AppConfig config, int depth)
    {
        items.Clear();

        if (!Directory.Exists(path))
        {
            items.Add(DisabledItem("(Folder not found)"));
            return;
        }

        items.Add(CreateCommandItem("Open folder", () => OpenPath(path)));
        items.Add(new Separator());

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
                items.Add(DisabledItem("(Empty)"));
                return;
            }

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                var item = new MenuItem
                {
                    Header = name,
                    Icon = CreateIcon(dir, isDirectory: true)
                };

                // Lazy-load submenu on open to keep first paint fast.
                var placeholder = new MenuItem { Header = "Loading…", IsEnabled = false };
                item.Items.Add(placeholder);
                item.SubmenuOpened += (_, _) =>
                {
                    if (item.Items.Count == 1 && ReferenceEquals(item.Items[0], placeholder))
                    {
                        if (depth >= 6)
                        {
                            item.Items.Clear();
                            item.Items.Add(CreateCommandItem("Open folder", () => OpenPath(dir)));
                        }
                        else
                        {
                            Populate(item.Items, dir, config, depth + 1);
                        }
                    }
                };

                items.Add(item);
            }

            foreach (var file in files)
            {
                var name = config.ShowFileExtensions
                    ? Path.GetFileName(file)
                    : Path.GetFileNameWithoutExtension(file);

                if (string.IsNullOrWhiteSpace(name))
                    name = Path.GetFileName(file);

                var item = CreateCommandItem(name, () => OpenPath(file));
                item.Icon = CreateIcon(file, isDirectory: false);
                items.Add(item);
            }

            var truncated = Directory.EnumerateDirectories(path, "*", options).Skip(config.MaxItemsPerMenu).Any()
                || Directory.EnumerateFiles(path, "*", options).Skip(config.MaxItemsPerMenu).Any();

            if (truncated)
            {
                items.Add(new Separator());
                items.Add(DisabledItem($"Showing first {config.MaxItemsPerMenu} items…"));
                items.Add(CreateCommandItem("Open folder to see all", () => OpenPath(path)));
            }
        }
        catch (Exception ex)
        {
            items.Add(DisabledItem($"Error: {ex.Message}"));
        }
    }

    private static ContextMenu CreateMenu()
    {
        return new ContextMenu
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
            StaysOpen = false
        };
    }

    private static MenuItem CreateCommandItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem DisabledItem(string header) =>
        new() { Header = header, IsEnabled = false };

    private static System.Windows.Controls.Image? CreateIcon(string path, bool isDirectory)
    {
        var source = IconService.GetIcon(path, isDirectory);
        if (source == null)
            return null;

        return new System.Windows.Controls.Image
        {
            Source = source,
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform
        };
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
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
}
