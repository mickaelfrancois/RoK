using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.Services.Accessibility;
using Windows.System;

namespace Rok.Commons;

public sealed partial class KeyVisualBox : UserControl
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(
        nameof(Shortcut),
        typeof(KeyboardShortcut),
        typeof(KeyVisualBox),
        new PropertyMetadata(null, OnShortcutChanged));

    public KeyboardShortcut? Shortcut
    {
        get => (KeyboardShortcut?)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    public KeyVisualBox()
    {
        InitializeComponent();
    }

    private static void OnShortcutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyVisualBox box && e.NewValue is KeyboardShortcut shortcut)
        {
            box.KeysHost.ItemsSource = BuildLabels(shortcut);
        }
    }

    private static List<string> BuildLabels(KeyboardShortcut shortcut)
    {
        List<string> labels = new();

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Control))
            labels.Add("Ctrl");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
            labels.Add("Shift");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
            labels.Add("Alt");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Windows))
            labels.Add("Win");

        labels.Add(FormatKey(shortcut.Key));

        return labels;
    }

    private static string FormatKey(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Right => "→",
            VirtualKey.Left => "←",
            VirtualKey.Up => "↑",
            VirtualKey.Down => "↓",
            VirtualKey.Space => "Space",
            VirtualKey.Escape => "Esc",
            VirtualKey.Number0 => "0",
            VirtualKey.Number1 => "1",
            VirtualKey.Number2 => "2",
            VirtualKey.Number3 => "3",
            VirtualKey.Number4 => "4",
            VirtualKey.Number5 => "5",
            VirtualKey.Number6 => "6",
            VirtualKey.Number7 => "7",
            VirtualKey.Number8 => "8",
            VirtualKey.Number9 => "9",
            VirtualKey.F1 => "F1",
            VirtualKey.F11 => "F11",
            _ => key.ToString()
        };
    }
}