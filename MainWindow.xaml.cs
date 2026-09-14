using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TaskbarFolders.Models;
using TaskbarFolders.Native;
using TaskbarFolders.Services;
using Forms = System.Windows.Forms;
using OpenFolderDialog = System.Windows.Forms.FolderBrowserDialog;

namespace TaskbarFolders;

public partial class MainWindow : Window
{
    private const int MinWidthPx = 48;
    private const int GripWidthPx = 6;

    private readonly TaskbarLocator _locator = new();
    private AppConfig _config;
    private bool _placed;
    private bool _embedded;
    private bool _resizing;
    private bool _resizeFromLeft;
    private double _resizeStartScreenX;
    private int _resizeStartWidthPx;
    private int _currentWidthPx;
    private Brush _foreground = Brushes.White;
    private Brush _mutedForeground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
    private bool? _appliedLightTheme;
    private DispatcherTimer? _embedRetry;
    private HwndSource? _hwndSource;
    private HwndSourceHook? _wndHook;

    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action? HideToolbarRequested;
    public event Action? ConfigChanged;

    public MainWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        Loaded += OnLoaded;
        SourceInitialized += (_, _) =>
        {
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            NativeMethods.PrepareChildWindow(this);
            NativeMethods.CleanupStaleRebarBands();
            NativeMethods.HideFromAppTaskbar(helper.Handle);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _wndHook = WndProc;
            _hwndSource?.AddHook(_wndHook);
        };
    }

    public void ApplyConfig(AppConfig config)
    {
        _config = config;
        Visibility = config.ShowToolbar ? Visibility.Visible : Visibility.Collapsed;
        ApplyTheme();
        UpdateGripLayout();
        RebuildButtons();
        AttachAndPosition();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
        UpdateGripLayout();
        RebuildButtons();
        _locator.TaskbarChanged += AttachAndPosition;
        _locator.ThemeChanged += OnSystemThemeChanged;
        _locator.Start();
        _placed = true;
        StartEmbedRetry();
        AttachAndPosition();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)NativeMethods.WmTaskbarCreated)
        {
            _embedded = false;
            StartEmbedRetry();
            AttachAndPosition();
        }

        return IntPtr.Zero;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ToolbarAutomationPeer(this);

    private sealed class ToolbarAutomationPeer : FrameworkElementAutomationPeer
    {
        public ToolbarAutomationPeer(MainWindow owner) : base(owner) { }

        protected override string GetClassNameCore() => "TaskbarFoldersToolbar";
        protected override string GetNameCore() => "";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override bool IsControlElementCore() => false;
        protected override bool IsContentElementCore() => false;
        protected override List<AutomationPeer> GetChildrenCore() => [];
    }

    private void OnSystemThemeChanged()
    {
        ApplyTheme(force: true);
        RebuildButtons();
    }

    protected override void OnClosed(EventArgs e)
    {
        StopEmbedRetry();
        if (_hwndSource != null && _wndHook != null)
            _hwndSource.RemoveHook(_wndHook);
        _locator.Stop();
        NativeMethods.CleanupStaleRebarBands();
        base.OnClosed(e);
    }

    private void ApplyTheme(bool force = false)
    {
        var light = NativeMethods.IsSystemLightTheme();
        if (!force && _appliedLightTheme == light)
            return;

        _appliedLightTheme = light;
        _foreground = light ? Brushes.Black : Brushes.White;
        _mutedForeground = new SolidColorBrush(light
            ? Color.FromRgb(0x55, 0x55, 0x55)
            : Color.FromRgb(0xBB, 0xBB, 0xBB));
        _mutedForeground.Freeze();

        Background = Brushes.Transparent;
        RootBorder.Background = Brushes.Transparent;

        // Light taskbar → dark dots; dark taskbar → light dots.
        var dotBrush = new SolidColorBrush(light
            ? Color.FromArgb(0xB3, 0x22, 0x22, 0x22)
            : Color.FromArgb(0xB3, 0xEE, 0xEE, 0xEE));
        dotBrush.Freeze();

        foreach (var grip in new[] { LeftGrip, RightGrip })
        {
            if (grip.Child is Canvas canvas)
            {
                foreach (var child in canvas.Children)
                {
                    if (child is System.Windows.Shapes.Ellipse el)
                        el.Fill = dotBrush;
                }
            }
        }

        SetGripHighlight(active: _resizing);
    }

    private void SetGripHighlight(bool active)
    {
        var light = _appliedLightTheme ?? NativeMethods.IsSystemLightTheme();
        Brush background = active
            ? new SolidColorBrush(light
                ? Color.FromArgb(0x33, 0, 0, 0)
                : Color.FromArgb(0x33, 255, 255, 255))
            : Brushes.Transparent;

        LeftGrip.Background = background;
        RightGrip.Background = background;
    }

    private void UpdateGripLayout()
    {
        // Grip on the free/inner edge so dragging feels like a Win10 toolbar handle.
        // Before tray → left grip; after task buttons → right grip; center → both.
        var alignment = _config.ToolbarAlignment;
        var left = alignment is 1 or 2;
        var right = alignment is 0 or 1;

        LeftGripColumn.Width = new GridLength(left ? GripWidthPx : 0);
        RightGripColumn.Width = new GridLength(right ? GripWidthPx : 0);
        LeftGrip.Visibility = left ? Visibility.Visible : Visibility.Collapsed;
        RightGrip.Visibility = right ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RebuildButtons()
    {
        FolderButtons.Children.Clear();

        if (_config.Folders.Count == 0)
        {
            FolderButtons.Children.Add(new TextBlock
            {
                Text = "Right-click → Add folder…",
                Foreground = _mutedForeground,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                FontSize = 12
            });
            return;
        }

        foreach (var folder in _config.Folders.ToList())
            FolderButtons.Children.Add(CreateFolderButton(folder));
    }

    private System.Windows.Controls.Button CreateFolderButton(FolderEntry folder)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var showNames = _config.ShowFolderNames;

        var icon = IconService.GetIcon(folder.Path, isDirectory: true);
        if (icon != null)
        {
            panel.Children.Add(new Image
            {
                Source = icon,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, showNames ? 6 : 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (showNames)
        {
            panel.Children.Add(new TextBlock
            {
                Text = folder.Name,
                Foreground = _foreground,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = showNames ? " »" : "»",
            Foreground = _mutedForeground,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Margin = showNames ? new Thickness(0) : new Thickness(2, 0, 0, 0)
        });

        var button = new System.Windows.Controls.Button
        {
            Content = panel,
            Tag = folder,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = showNames ? new Thickness(8, 2, 8, 2) : new Thickness(6, 2, 6, 2),
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = showNames ? folder.Path : folder.Name
        };

        button.Click += FolderButton_Click;
        return button;
    }

    private Forms.ContextMenuStrip? _openFolderMenu;
    private string? _openFolderId;
    private DateTime _folderMenuClosedAt;

    private void FolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not FolderEntry folder)
            return;

        if (_openFolderMenu is { IsDisposed: false, Visible: true } && _openFolderId == folder.Id)
        {
            _openFolderMenu.Close();
            return;
        }

        // Clicking the toolbar button dismisses the menu first, then raises Click — don't reopen.
        if (_openFolderId == folder.Id && DateTime.UtcNow - _folderMenuClosedAt < TimeSpan.FromMilliseconds(300))
            return;

        if (_openFolderMenu is { IsDisposed: false, Visible: true })
            _openFolderMenu.Close();

        var menu = FolderMenuBuilder.Build(folder, _config);
        _openFolderMenu = menu;
        _openFolderId = folder.Id;
        menu.Closed += (_, _) =>
        {
            if (!ReferenceEquals(_openFolderMenu, menu))
                return;
            _openFolderMenu = null;
            _folderMenuClosedAt = DateTime.UtcNow;
        };

        var screen = button.PointToScreen(new Point(0, 0));
        menu.Show(
            new System.Drawing.Point((int)Math.Round(screen.X), (int)Math.Round(screen.Y)),
            Forms.ToolStripDropDownDirection.AboveRight);
    }

    private int MeasureContentWidthPx()
    {
        var dpi = NativeMethods.GetDpiScale(new WindowInteropHelper(this).Handle);
        FolderButtons.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var grips = 0;
        if (LeftGrip.Visibility == Visibility.Visible) grips += GripWidthPx;
        if (RightGrip.Visibility == Visibility.Visible) grips += GripWidthPx;
        var widthDip = Math.Max(FolderButtons.DesiredSize.Width + grips + 4, MinWidthPx);
        return (int)Math.Ceiling(widthDip * dpi);
    }

    private int ResolveWidthPx()
    {
        var content = MeasureContentWidthPx();
        if (_config.ToolbarWidthPx > 0)
            return Math.Max(MinWidthPx, _config.ToolbarWidthPx);
        return content;
    }

    private void AttachAndPosition()
    {
        if (_resizing)
            return;
        if (!_placed && !IsLoaded)
            return;
        if (!_config.ShowToolbar)
            return;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        ShowInTaskbar = false;
        Title = "";
        var tray = NativeMethods.GetTaskbarHwnd();
        if (!_embedded || tray == IntPtr.Zero || NativeMethods.GetParent(hwnd) != tray)
        {
            _embedded = NativeMethods.IsTaskbarReady() && NativeMethods.TryEmbedInTaskbar(this);
            if (!_embedded)
            {
                NativeMethods.HideFromAppTaskbar(hwnd);
                NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, -32000, -32000, 1, 1,
                    NativeMethods.SwpNoactivate | NativeMethods.SwpNozorder);
                StartEmbedRetry();
                return;
            }
        }
        else
        {
            NativeMethods.PrepareChildWindow(this);
            NativeMethods.SetWindowText(hwnd, "");
            NativeMethods.HideFromAppTaskbar(hwnd);
            NativeMethods.HideOwnerFromTaskbar(hwnd);
        }

        StopEmbedRetry();
        UpdateGripLayout();
        var widthPx = ResolveWidthPx();
        _currentWidthPx = widthPx;
        ApplyWindowSize(widthPx);
    }

    private void StartEmbedRetry()
    {
        if (_embedRetry != null)
            return;

        _embedRetry = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _embedRetry.Tick += (_, _) => AttachAndPosition();
        _embedRetry.Start();
    }

    private void StopEmbedRetry()
    {
        _embedRetry?.Stop();
        _embedRetry = null;
    }

    private void ApplyWindowSize(int widthPx)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var slots = NativeMethods.GetTaskbarSlots();
        var heightPx = Math.Max(slots.TrayClient.Height, 40);
        var trayLeft = TaskbarLayout.TryGetSystemTrayLeft(slots.TrayHwnd, out var uiaTrayLeft)
            ? uiaTrayLeft
            : slots.Notify?.Left ?? slots.TrayClient.Width;
        var maxWidth = Math.Max(MinWidthPx, trayLeft - 16);
        widthPx = Math.Clamp(widthPx, MinWidthPx, maxWidth);
        _currentWidthPx = widthPx;

        var offsetPx = (int)(_config.ToolbarOffsetX * NativeMethods.GetDpiScale(hwnd));
        var x = TaskbarLayout.ReserveToolbarSlot(slots, widthPx, _config.ToolbarAlignment, offsetPx);

        Topmost = false;
        NativeMethods.PositionChild(hwnd, x, 0, widthPx, heightPx);
    }

    private void Grip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement grip)
            return;

        _resizing = true;
        _resizeFromLeft = ReferenceEquals(grip, LeftGrip);
        _resizeStartScreenX = PointToScreen(e.GetPosition(this)).X;
        _resizeStartWidthPx = _currentWidthPx > 0 ? _currentWidthPx : ResolveWidthPx();
        SetGripHighlight(active: true);
        grip.CaptureMouse();
        e.Handled = true;
    }

    private void Grip_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_resizing || e.LeftButton != MouseButtonState.Pressed)
            return;

        var screenX = PointToScreen(e.GetPosition(this)).X;
        var delta = (int)Math.Round(screenX - _resizeStartScreenX);

        // Left grip: drag left ⇒ wider; right grip: drag right ⇒ wider.
        var width = _resizeFromLeft
            ? _resizeStartWidthPx - delta
            : _resizeStartWidthPx + delta;

        ApplyWindowSize(width);
        e.Handled = true;
    }

    private void Grip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing)
            return;

        EndResize(sender as FrameworkElement);
        e.Handled = true;
    }

    private void Grip_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_resizing)
            EndResize(sender as FrameworkElement);
    }

    private void EndResize(FrameworkElement? grip)
    {
        _resizing = false;
        grip?.ReleaseMouseCapture();
        SetGripHighlight(active: false);

        _config.ToolbarWidthPx = _currentWidthPx;
        ConfigService.Save(_config);
        ConfigChanged?.Invoke();
        AttachAndPosition();
    }

    private void ResetWidth_Click(object sender, RoutedEventArgs e)
    {
        _config.ToolbarWidthPx = 0;
        ConfigService.Save(_config);
        ConfigChanged?.Invoke();
        AttachAndPosition();
    }

    private void RootBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var menu = FolderMenuBuilder.CreateThemedStrip(showImageMargin: false);
        menu.Items.Add(FolderMenuBuilder.CreateCommandItem("Add folder…", () => AddFolder_Click(this, e)));
        menu.Items.Add(FolderMenuBuilder.CreateCommandItem("Settings…", () => Settings_Click(this, e)));
        menu.Items.Add(FolderMenuBuilder.CreateCommandItem("Reset width to fit", () => ResetWidth_Click(this, e)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(FolderMenuBuilder.CreateCommandItem("Hide toolbar", () => HideToolbar_Click(this, e)));
        menu.Items.Add(FolderMenuBuilder.CreateCommandItem("Exit", () => Exit_Click(this, e)));
        menu.Show(Forms.Cursor.Position);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFolderDialog
        {
            Description = "Choose a folder to pin to the toolbar",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            return;

        if (_config.Folders.Any(f => string.Equals(f.Path, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("That folder is already on the toolbar.", "Taskbar Folders",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _config.Folders.Add(new FolderEntry { Path = dialog.SelectedPath });
        ConfigService.Save(_config);
        RebuildButtons();
        AttachAndPosition();
        ConfigChanged?.Invoke();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void HideToolbar_Click(object sender, RoutedEventArgs e) => HideToolbarRequested?.Invoke();
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();
}
