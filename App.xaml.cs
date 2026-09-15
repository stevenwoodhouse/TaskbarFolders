using System.Windows;
using System.Windows.Threading;
using TaskbarFolders.Models;
using TaskbarFolders.Native;
using TaskbarFolders.Services;
using Forms = System.Windows.Forms;

namespace TaskbarFolders;

public partial class App : System.Windows.Application
{
    private AppConfig _config = null!;
    private MainWindow? _toolbar;
    private Forms.NotifyIcon? _tray;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _toolbarWaitTimer;
    private int _toolbarWaitAttempts;
    private ShellMessageWindow? _shellMessages;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _config = ConfigService.Load();
        AutostartService.SetEnabled(_config.StartWithWindows);

        _shellMessages = new ShellMessageWindow();
        _shellMessages.TaskbarCreated += OnTaskbarCreated;
        _shellMessages.ThemeChanged += OnOsThemeChanged;

        UpdateTrayIcon();
        ShowToolbar();

        if (!_config.ShowToolbar && !_config.ShowTrayIcon)
        {
            _config.ShowTrayIcon = true;
            ConfigService.Save(_config);
            UpdateTrayIcon();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _toolbarWaitTimer?.Stop();
        _toolbarWaitTimer = null;
        _shellMessages?.Dispose();
        _shellMessages = null;
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

        // At logon the Run key can start us before Explorer finishes the taskbar.
        // Showing then registers a normal app button on the right of the taskbar.
        if (!NativeMethods.IsTaskbarReady())
        {
            if (_toolbarWaitTimer == null)
            {
                _toolbarWaitAttempts = 0;
                _toolbarWaitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _toolbarWaitTimer.Tick += OnToolbarWaitTick;
                _toolbarWaitTimer.Start();
            }
            return;
        }

        CreateAndShowToolbar();
    }

    private void OnToolbarWaitTick(object? sender, EventArgs e)
    {
        _toolbarWaitAttempts++;
        if (!NativeMethods.IsTaskbarReady() && _toolbarWaitAttempts < 200)
            return;

        _toolbarWaitTimer?.Stop();
        _toolbarWaitTimer = null;
        CreateAndShowToolbar();
    }

    private void CreateAndShowToolbar()
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
        _toolbar.Closed += OnToolbarClosed;
        _toolbar.ConfigChanged += () =>
        {
            _config = ConfigService.Load();
            UpdateTrayMenu();
        };
        _toolbar.ApplyConfig(_config);
        _toolbar.Show();
    }

    private void OnToolbarClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_toolbar, sender))
            _toolbar = null;
    }

    private void OnTaskbarCreated()
    {
        if (_exiting)
            return;

        _toolbarWaitTimer?.Stop();
        _toolbarWaitTimer = null;
        TaskbarLayout.Invalidate();
        if (_toolbar != null)
            _toolbar.ReattachToTaskbar();
        else
            ShowToolbar();

        UpdateTrayIcon();
    }

    private void OnOsThemeChanged()
    {
        _toolbar?.ApplySystemTheme();
        UpdateTrayMenu();
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

        _tray.ContextMenuStrip?.Dispose();
        var menu = FolderMenuBuilder.CreateThemedStrip(showImageMargin: false, disposeAfterClose: false);

        foreach (var folder in _config.Folders)
        {
            var item = FolderMenuBuilder.CreateCommandItem(folder.Name, () => OpenFolderMenuFromTray(folder));
            menu.Items.Add(item);
        }

        if (_config.Folders.Count > 0)
            menu.Items.Add(new Forms.ToolStripSeparator());

        menu.Items.Add(FolderMenuBuilder.CreateCommandItem("Settings…", OpenSettings));
        menu.Items.Add(FolderMenuBuilder.CreateCommandItem(_config.ShowToolbar ? "Hide toolbar" : "Show toolbar", ToggleToolbar));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(FolderMenuBuilder.CreateCommandItem("Exit", ExitApp));

        _tray.ContextMenuStrip = menu;
    }

    private void OpenFolderMenuFromTray(FolderEntry folder)
    {
        var menu = FolderMenuBuilder.Build(folder, _config);
        menu.Show(Forms.Cursor.Position);
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
        _exiting = true;
        _shellMessages?.Dispose();
        _shellMessages = null;
        _tray?.Dispose();
        _tray = null;
        _toolbar?.Close();
        Shutdown();
    }
}
