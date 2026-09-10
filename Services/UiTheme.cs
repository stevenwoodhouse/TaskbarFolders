using System.Drawing;
using System.Windows;
using System.Windows.Media;
using TaskbarFolders.Native;
using Media = System.Windows.Media;
using DrawingColor = System.Drawing.Color;

namespace TaskbarFolders.Services;

internal sealed class UiTheme
{
    public bool IsLight { get; private init; }
    public DrawingColor Back { get; private init; }
    public DrawingColor Text { get; private init; }
    public DrawingColor DisabledText { get; private init; }
    public DrawingColor Hot { get; private init; }
    public DrawingColor Border { get; private init; }
    public DrawingColor Separator { get; private init; }
    public DrawingColor ControlBack { get; private init; }
    public DrawingColor ButtonBack { get; private init; }

    public SolidColorBrush WindowBrush { get; private init; } = null!;
    public SolidColorBrush TextBrush { get; private init; } = null!;
    public SolidColorBrush MutedBrush { get; private init; } = null!;
    public SolidColorBrush ControlBrush { get; private init; } = null!;
    public SolidColorBrush BorderBrush { get; private init; } = null!;
    public SolidColorBrush HotBrush { get; private init; } = null!;
    public SolidColorBrush ButtonBrush { get; private init; } = null!;

    public static UiTheme Current()
    {
        var light = NativeMethods.IsSystemLightTheme();
        if (light)
        {
            return Create(
                isLight: true,
                back: DrawingColor.FromArgb(243, 243, 243),
                text: DrawingColor.FromArgb(32, 32, 32),
                disabled: DrawingColor.FromArgb(140, 140, 140),
                hot: DrawingColor.FromArgb(232, 232, 232),
                border: DrawingColor.FromArgb(218, 218, 218),
                separator: DrawingColor.FromArgb(218, 218, 218),
                control: DrawingColor.FromArgb(255, 255, 255),
                button: DrawingColor.FromArgb(251, 251, 251));
        }

        return Create(
            isLight: false,
            back: DrawingColor.FromArgb(32, 32, 32),
            text: DrawingColor.FromArgb(255, 255, 255),
            disabled: DrawingColor.FromArgb(160, 160, 160),
            hot: DrawingColor.FromArgb(61, 61, 61),
            border: DrawingColor.FromArgb(64, 64, 64),
            separator: DrawingColor.FromArgb(80, 80, 80),
            control: DrawingColor.FromArgb(44, 44, 44),
            button: DrawingColor.FromArgb(55, 55, 55));
    }

    public void ApplyResources(FrameworkElement element)
    {
        element.Resources["WindowBack"] = WindowBrush;
        element.Resources["Text"] = TextBrush;
        element.Resources["Muted"] = MutedBrush;
        element.Resources["ControlBack"] = ControlBrush;
        element.Resources["Border"] = BorderBrush;
        element.Resources["Hot"] = HotBrush;
        element.Resources["ButtonBack"] = ButtonBrush;
    }

    public void ApplyToWindow(Window window)
    {
        ApplyResources(window);
        window.Background = WindowBrush;
        window.Foreground = TextBrush;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
            NativeMethods.SetImmersiveDarkMode(hwnd, !IsLight);
    }

    private static UiTheme Create(
        bool isLight,
        DrawingColor back,
        DrawingColor text,
        DrawingColor disabled,
        DrawingColor hot,
        DrawingColor border,
        DrawingColor separator,
        DrawingColor control,
        DrawingColor button)
    {
        return new UiTheme
        {
            IsLight = isLight,
            Back = back,
            Text = text,
            DisabledText = disabled,
            Hot = hot,
            Border = border,
            Separator = separator,
            ControlBack = control,
            ButtonBack = button,
            WindowBrush = Brush(back),
            TextBrush = Brush(text),
            MutedBrush = Brush(disabled),
            ControlBrush = Brush(control),
            BorderBrush = Brush(border),
            HotBrush = Brush(hot),
            ButtonBrush = Brush(button)
        };
    }

    private static SolidColorBrush Brush(DrawingColor color)
    {
        var brush = new SolidColorBrush(Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
