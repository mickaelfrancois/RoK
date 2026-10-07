using Rok.Services.Accessibility;

namespace Rok.PresentationTests.Accessibility;

public class KeyboardShortcutFormatterTests
{
    [Fact(DisplayName = "format_renders_control_and_arrow_as_ctrl_plus_arrow")]
    public void Format_renders_control_and_arrow_as_ctrl_plus_arrow()
    {
        KeyboardShortcut shortcut = KeyboardShortcutCatalog.ById(ShortcutId.Next);

        string result = KeyboardShortcutFormatter.Format(shortcut);

        Assert.Equal("Ctrl+→", result);
    }

    [Fact(DisplayName = "format_orders_ctrl_before_shift")]
    public void Format_orders_ctrl_before_shift()
    {
        KeyboardShortcut shortcut = KeyboardShortcutCatalog.ById(ShortcutId.ToggleCompact);

        string result = KeyboardShortcutFormatter.Format(shortcut);

        Assert.Equal("Ctrl+Shift+M", result);
    }

    [Theory(DisplayName = "format_renders_shortcut_without_modifier_as_key_only")]
    [InlineData(ShortcutId.PlayPause, "Space")]
    [InlineData(ShortcutId.ToggleFullScreen, "F11")]
    public void Format_renders_shortcut_without_modifier_as_key_only(ShortcutId id, string expected)
    {
        KeyboardShortcut shortcut = KeyboardShortcutCatalog.ById(id);

        string result = KeyboardShortcutFormatter.Format(shortcut);

        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "parts_are_non_empty_and_end_with_the_key_for_every_catalog_shortcut")]
    public void Parts_are_non_empty_and_end_with_the_key_for_every_catalog_shortcut()
    {
        string[] modifierOrder = ["Ctrl", "Shift", "Alt", "Win"];

        Assert.All(KeyboardShortcutCatalog.All, shortcut =>
        {
            IReadOnlyList<string> parts = KeyboardShortcutFormatter.Parts(shortcut);

            Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p)));
            Assert.DoesNotContain(parts[^1], modifierOrder);

            int[] indexes = parts.Take(parts.Count - 1).Select(p => Array.IndexOf(modifierOrder, p)).ToArray();

            Assert.DoesNotContain(-1, indexes);
            Assert.Equal(indexes.OrderBy(i => i), indexes);
        });
    }

    [Fact(DisplayName = "with_shortcut_appends_the_formatted_shortcut_in_parentheses")]
    public void With_shortcut_appends_the_formatted_shortcut_in_parentheses()
    {
        string result = KeyboardShortcutFormatter.WithShortcut("Next track", ShortcutId.Next);

        Assert.Equal("Next track (Ctrl+→)", result);
    }

    [Theory(DisplayName = "with_shortcut_returns_the_label_alone_when_shortcut_text_is_empty")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void With_shortcut_returns_the_label_alone_when_shortcut_text_is_empty(string? shortcutText)
    {
        string result = KeyboardShortcutFormatter.WithShortcut("Next track", shortcutText);

        Assert.Equal("Next track", result);
    }
}