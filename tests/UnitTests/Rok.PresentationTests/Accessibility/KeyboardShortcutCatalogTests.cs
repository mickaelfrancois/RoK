using Rok.Services.Accessibility;
using Windows.System;

namespace Rok.PresentationTests.Accessibility;

public class KeyboardShortcutCatalogTests
{
    [Fact(DisplayName = "catalog_should_contain_all_expected_ids")]
    public void Catalog_should_contain_all_expected_ids()
    {
        ShortcutId[] expected = Enum.GetValues<ShortcutId>();

        ShortcutId[] actual = KeyboardShortcutCatalog.All.Select(s => s.Id).ToArray();

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    [Fact(DisplayName = "catalog_should_contain_no_duplicate_id")]
    public void Catalog_should_contain_no_duplicate_id()
    {
        IEnumerable<IGrouping<ShortcutId, KeyboardShortcut>> duplicates =
            KeyboardShortcutCatalog.All.GroupBy(s => s.Id).Where(g => g.Count() > 1);

        Assert.Empty(duplicates);
    }

    [Fact(DisplayName = "catalog_should_contain_no_duplicate_modifiers_and_key_combination")]
    public void Catalog_should_contain_no_duplicate_modifiers_and_key_combination()
    {
        IEnumerable<IGrouping<(VirtualKeyModifiers, VirtualKey), KeyboardShortcut>> duplicates =
            KeyboardShortcutCatalog.All.GroupBy(s => (s.Modifiers, s.Key)).Where(g => g.Count() > 1);

        Assert.Empty(duplicates);
    }

    [Fact(DisplayName = "each_shortcut_should_have_non_empty_label_resource_key")]
    public void Each_shortcut_should_have_non_empty_label_resource_key()
    {
        Assert.All(KeyboardShortcutCatalog.All, s =>
            Assert.False(string.IsNullOrWhiteSpace(s.LabelResourceKey)));
    }

    [Fact(DisplayName = "by_id_should_return_expected_shortcut")]
    public void By_id_should_return_expected_shortcut()
    {
        KeyboardShortcut shortcut = KeyboardShortcutCatalog.ById(ShortcutId.PlayPause);

        Assert.Equal(ShortcutId.PlayPause, shortcut.Id);
        Assert.Equal(VirtualKey.Space, shortcut.Key);
        Assert.Equal(VirtualKeyModifiers.None, shortcut.Modifiers);
    }

    [Fact(DisplayName = "by_id_should_throw_when_id_not_present")]
    public void By_id_should_throw_when_id_not_present()
    {
        Assert.Throws<KeyNotFoundException>(() => KeyboardShortcutCatalog.ById((ShortcutId)999));
    }

    [Fact(DisplayName = "by_category_should_group_all_shortcuts_correctly")]
    public void By_category_should_group_all_shortcuts_correctly()
    {
        int total = 0;

        foreach (ShortcutCategory category in Enum.GetValues<ShortcutCategory>())
        {
            total += KeyboardShortcutCatalog.ByCategory(category).Count();
        }

        Assert.Equal(KeyboardShortcutCatalog.All.Count, total);
    }
}