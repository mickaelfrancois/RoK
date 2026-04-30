using Microsoft.UI.Xaml.Controls;
using Rok.Services.Accessibility;

namespace Rok.Commons;

public sealed partial class KeyboardShortcutsView : UserControl
{
    public KeyboardShortcutsView()
    {
        InitializeComponent();
        GroupsHost.ItemsSource = BuildGroups();
    }

    private static List<ShortcutGroupViewModel> BuildGroups()
    {
        ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();

        ShortcutCategory[] categoriesInOrder = new[]
        {
            ShortcutCategory.Playback,
            ShortcutCategory.Navigation,
            ShortcutCategory.Modes,
            ShortcutCategory.Help
        };

        List<ShortcutGroupViewModel> groups = new();

        foreach (ShortcutCategory category in categoriesInOrder)
        {
            List<ShortcutRowViewModel> rows = new();

            foreach (KeyboardShortcut shortcut in KeyboardShortcutCatalog.ByCategory(category))
            {
                rows.Add(new ShortcutRowViewModel
                {
                    Label = loader.GetString(shortcut.LabelResourceKey),
                    Shortcut = shortcut
                });
            }

            groups.Add(new ShortcutGroupViewModel
            {
                Title = loader.GetString($"KeyboardShortcut_Dialog_Group_{category}"),
                Shortcuts = rows
            });
        }

        return groups;
    }
}

internal sealed class ShortcutGroupViewModel
{
    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<ShortcutRowViewModel> Shortcuts { get; init; } = new List<ShortcutRowViewModel>();
}

internal sealed class ShortcutRowViewModel
{
    public string Label { get; init; } = string.Empty;

    public KeyboardShortcut? Shortcut { get; init; }
}
