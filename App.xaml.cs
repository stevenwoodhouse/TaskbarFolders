using System.Windows;
using TaskbarFolders.Models;
using TaskbarFolders.Services;
using Forms = System.Windows.Forms;

namespace TaskbarFolders;

public partial class App : System.Windows.Application
{
    private AppConfig _config = null!;
    private MainWindow? _toolbar;
    private Forms.NotifyIcon? _tray;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _config = ConfigService.Load();
        AutostartService.SetEnabled(_config.StartWithWindows);

        ShowToolbar();
        UpdateTrayIcon();

        if (!_config.ShowToolbar && !_config.ShowTrayIcon)
        {
            _config.ShowTrayIcon = true;
            ConfigService.Save(_config);
            UpdateTrayIcon();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _toolbar?.Close();
        base.OnExit(e);
    }

    private void ShowToolbar()
    {
        if (_toolbar != null)
        {
            _toolbar.ApplyConfig(_config);
            return;
        }

        _toolbar = new MainWindow(_config);
        _toolbar.SettingsRequested += OpenSettings;
        _toolbar.ExitRequested += ExitApp;
        _toolbar.HideToolbarRequested += HideToolbar;
        _toolbar.ConfigChanged += () =>
        {
            _config = ConfigService.Load();
            UpdateTrayMenu();
        };
        _toolbar.ApplyConfig(_config);
        _toolbar.Show();
    }

    private void HideToolbar()
    {
        _config.ShowToolbar = false;
        if (!_config.ShowTrayIcon)
            _config.ShowTrayIcon = true;

        ConfigService.Save(_config);
        _toolbar?.ApplyConfig(_config);
        UpdateTrayIcon();
    }

    private void UpdateTrayIcon()
    {
        if (!_config.ShowTrayIcon)
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            return;
        }

        if (_tray == null)
        {
            _tray = new Forms.NotifyIcon
            {
                Text = "Taskbar Folders",
                Icon = IconService.GetAppIcon(),
                Visible = true
            };
            _tray.DoubleClick += (_, _) => OpenSettings();
        }

        _tray.Visible = true;
        UpdateTrayMenu();
    }

    private void UpdateTrayMenu()
    {
        if (_tray == null)
            return;

        var menu = new Forms.ContextMenuStrip();

        foreach (var folder in _config.Folders)
        {
            var item = new Forms.ToolStripMenuItem(folder.Name);
            item.Click += (_, _) => OpenFolderMenuFromTray(folder);
            menu.Items.Add(item);
        }

        if (_config.Folders.Count > 0)
            menu.Items.Add(new Forms.ToolStripSeparator());

        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(_config.ShowToolbar ? "Hide toolbar" : "Show toolbar", null, (_, _) => ToggleToolbar());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray.ContextMenuStrip = menu;
    }

    private void OpenFolderMenuFromTray(FolderEntry folder)
    {
        // Show a WPF menu at the cursor via a temporary invisible window.
        var menu = FolderMenuBuilder.Build(folder, _config);
        var helper = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            Width = 1,
            Height = 1,
            Left = Forms.Cursor.Position.X / GetDpiScale(),
            Top = Forms.Cursor.Position.Y / GetDpiScale(),
            Topmost = true
        };
        helper.Show();
        menu.Closed += (_, _) => helper.Close();
        menu.PlacementTarget = helper;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        helper.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private static double GetDpiScale()
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

    private void ToggleToolbar()
    {
        _config.ShowToolbar = !_config.ShowToolbar;
        if (!_config.ShowToolbar && !_config.ShowTrayIcon)
            _config.ShowTrayIcon = true;

        ConfigService.Save(_config);
        ShowToolbar();
        UpdateTrayIcon();
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_config);
        _settingsWindow.Owner = _toolbar;
        try
        {
            var result = _settingsWindow.ShowDialog();
            if (result == true)
            {
                _config = _settingsWindow.ResultConfig;
                ConfigService.Save(_config);
                AutostartService.SetEnabled(_config.StartWithWindows);
                ShowToolbar();
                UpdateTrayIcon();
            }
        }
        finally
        {
            _settingsWindow = null;
        }
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }
}
