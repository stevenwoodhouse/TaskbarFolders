using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TaskbarFolders.Models;
using TaskbarFolders.Services;
using Forms = System.Windows.Forms;

namespace TaskbarFolders;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly ObservableCollection<FolderEntry> _folders;

    public AppConfig ResultConfig { get; private set; }

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        _config = Clone(config);
        ResultConfig = _config;
        _folders = new ObservableCollection<FolderEntry>(_config.Folders);
        FolderList.ItemsSource = _folders;

        StartWithWindows.IsChecked = _config.StartWithWindows;
        ShowToolbar.IsChecked = _config.ShowToolbar;
        ShowTrayIcon.IsChecked = _config.ShowTrayIcon;
        ShowHiddenFiles.IsChecked = _config.ShowHiddenFiles;
        ShowFileExtensions.IsChecked = _config.ShowFileExtensions;
        ShowFolderNames.IsChecked = _config.ShowFolderNames;
        Alignment.SelectedIndex = Math.Clamp(_config.ToolbarAlignment, 0, 2);
        MaxItems.Text = _config.MaxItemsPerMenu.ToString();

        var theme = UiTheme.Current();
        theme.ApplyToWindow(this);
        SourceInitialized += (_, _) => theme.ApplyToWindow(this);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose a folder to pin",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            return;

        if (_folders.Any(f => string.Equals(f.Path, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "That folder is already pinned.", "Taskbar Folders",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _folders.Add(new FolderEntry { Path = dialog.SelectedPath });
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is FolderEntry entry)
            _folders.Remove(entry);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var index = FolderList.SelectedIndex;
        if (index <= 0)
            return;
        _folders.Move(index, index - 1);
        FolderList.SelectedIndex = index - 1;
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var index = FolderList.SelectedIndex;
        if (index < 0 || index >= _folders.Count - 1)
            return;
        _folders.Move(index, index + 1);
        FolderList.SelectedIndex = index + 1;
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is not FolderEntry entry)
            return;

        var name = Prompt("Display name for this folder:", entry.Name);
        if (name == null)
            return;

        entry.DisplayName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        FolderList.Items.Refresh();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxItems.Text.Trim(), out var maxItems) || maxItems < 5 || maxItems > 200)
        {
            MessageBox.Show(this, "Max items per menu must be a number between 5 and 200.",
                "Taskbar Folders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ShowToolbar.IsChecked != true && ShowTrayIcon.IsChecked != true)
        {
            MessageBox.Show(this, "Keep at least the toolbar or the tray icon enabled.",
                "Taskbar Folders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.Folders = _folders.ToList();
        _config.StartWithWindows = StartWithWindows.IsChecked == true;
        _config.ShowToolbar = ShowToolbar.IsChecked == true;
        _config.ShowTrayIcon = ShowTrayIcon.IsChecked == true;
        _config.ShowHiddenFiles = ShowHiddenFiles.IsChecked == true;
        _config.ShowFileExtensions = ShowFileExtensions.IsChecked == true;
        _config.ShowFolderNames = ShowFolderNames.IsChecked == true;
        _config.ToolbarAlignment = Alignment.SelectedIndex;
        _config.MaxItemsPerMenu = maxItems;

        ResultConfig = _config;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static AppConfig Clone(AppConfig source) => new()
    {
        StartWithWindows = source.StartWithWindows,
        ShowToolbar = source.ShowToolbar,
        ShowTrayIcon = source.ShowTrayIcon,
        MaxItemsPerMenu = source.MaxItemsPerMenu,
        ShowHiddenFiles = source.ShowHiddenFiles,
        ShowFileExtensions = source.ShowFileExtensions,
        ShowFolderNames = source.ShowFolderNames,
        ToolbarAlignment = source.ToolbarAlignment,
        ToolbarOffsetX = source.ToolbarOffsetX,
        ToolbarWidthPx = source.ToolbarWidthPx,
        Folders = source.Folders.Select(f => new FolderEntry
        {
            Id = f.Id,
            Path = f.Path,
            DisplayName = f.DisplayName
        }).ToList()
    };

    private string? Prompt(string message, string initial)
    {
        var theme = UiTheme.Current();
        var dialog = new Window
        {
            Title = "Rename folder",
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = theme.WindowBrush,
            Foreground = theme.TextBrush
        };
        theme.ApplyToWindow(dialog);
        dialog.SourceInitialized += (_, _) => theme.ApplyToWindow(dialog);

        var box = new TextBox
        {
            Text = initial,
            Margin = new Thickness(16, 8, 16, 8),
            Background = theme.ControlBrush,
            Foreground = theme.TextBrush,
            BorderBrush = theme.BorderBrush,
            CaretBrush = theme.TextBrush
        };

        var ok = new Button
        {
            Content = "OK",
            Width = 80,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
            Background = theme.ButtonBrush,
            Foreground = theme.TextBrush,
            BorderBrush = theme.BorderBrush
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Width = 80,
            Height = 28,
            IsCancel = true,
            Background = theme.ButtonBrush,
            Foreground = theme.TextBrush,
            BorderBrush = theme.BorderBrush
        };

        string? result = null;
        ok.Click += (_, _) =>
        {
            result = box.Text;
            dialog.DialogResult = true;
            dialog.Close();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 8, 16, 16)
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var layout = new StackPanel();
        layout.Children.Add(new TextBlock
        {
            Text = message,
            Margin = new Thickness(16, 16, 16, 8),
            Foreground = theme.TextBrush
        });
        layout.Children.Add(box);
        layout.Children.Add(buttons);
        dialog.Content = layout;

        return dialog.ShowDialog() == true ? result : null;
    }
}
