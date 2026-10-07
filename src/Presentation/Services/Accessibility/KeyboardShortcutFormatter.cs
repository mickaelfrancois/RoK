using Windows.System;

namespace Rok.Services.Accessibility;

/// <summary>
/// Renders keyboard shortcuts as text, shared by the shortcuts dialog and the command tooltips.
/// </summary>
public static class KeyboardShortcutFormatter
{
    /// <summary>
    /// Splits a shortcut into its displayed parts: modifiers in the order Ctrl, Shift, Alt, Win, then the key.
    /// </summary>
    public static IReadOnlyList<string> Parts(KeyboardShortcut shortcut)
    {
        List<string> parts = new();

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Control))
            parts.Add("Ctrl");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
            parts.Add("Shift");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
            parts.Add("Alt");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Windows))
            parts.Add("Win");

        parts.Add(FormatKey(shortcut.Key));

        return parts;
    }

    /// <summary>
    /// Formats a shortcut as a single string, for example <c>Ctrl+Shift+M</c>.
    /// </summary>
    public static string Format(KeyboardShortcut shortcut)
    {
        return string.Join("+", Parts(shortcut));
    }

    /// <summary>
    /// Composes "label (shortcut)" using the catalog entry of <paramref name="id"/>.
    /// </summary>
    public static string WithShortcut(string label, ShortcutId id)
    {
        return WithShortcut(label, Format(KeyboardShortcutCatalog.ById(id)));
    }

    /// <summary>
    /// Composes "label (shortcut)", or the label alone when the shortcut text is empty.
    /// </summary>
    public static string WithShortcut(string label, string? shortcutText)
    {
        if (string.IsNullOrWhiteSpace(shortcutText))
            return label;

        return $"{label} ({shortcutText})";
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