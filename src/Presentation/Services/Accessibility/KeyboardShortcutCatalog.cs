using Windows.System;

namespace Rok.Services.Accessibility;

public static class KeyboardShortcutCatalog
{
    private static readonly IReadOnlyList<KeyboardShortcut> _shortcuts = new List<KeyboardShortcut>
    {
        new KeyboardShortcut(ShortcutId.PlayPause, ShortcutCategory.Playback, VirtualKeyModifiers.None, VirtualKey.Space, "KeyboardShortcut_Action_PlayPause"),
        new KeyboardShortcut(ShortcutId.Next, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.Right, "KeyboardShortcut_Action_Next"),
        new KeyboardShortcut(ShortcutId.Previous, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.Left, "KeyboardShortcut_Action_Previous"),
        new KeyboardShortcut(ShortcutId.VolumeUp, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.Up, "KeyboardShortcut_Action_VolumeUp"),
        new KeyboardShortcut(ShortcutId.VolumeDown, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.Down, "KeyboardShortcut_Action_VolumeDown"),
        new KeyboardShortcut(ShortcutId.Mute, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.M, "KeyboardShortcut_Action_Mute"),
        new KeyboardShortcut(ShortcutId.Shuffle, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.H, "KeyboardShortcut_Action_Shuffle"),
        new KeyboardShortcut(ShortcutId.Repeat, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.T, "KeyboardShortcut_Action_Repeat"),
        new KeyboardShortcut(ShortcutId.SeekForward, ShortcutCategory.Playback, VirtualKeyModifiers.Shift, VirtualKey.Right, "KeyboardShortcut_Action_SeekForward"),
        new KeyboardShortcut(ShortcutId.SeekBackward, ShortcutCategory.Playback, VirtualKeyModifiers.Shift, VirtualKey.Left, "KeyboardShortcut_Action_SeekBackward"),

        new KeyboardShortcut(ShortcutId.OpenAlbums, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number1, "KeyboardShortcut_Action_OpenAlbums"),
        new KeyboardShortcut(ShortcutId.OpenArtists, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number2, "KeyboardShortcut_Action_OpenArtists"),
        new KeyboardShortcut(ShortcutId.OpenTracks, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number3, "KeyboardShortcut_Action_OpenTracks"),
        new KeyboardShortcut(ShortcutId.OpenPlaylists, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number4, "KeyboardShortcut_Action_OpenPlaylists"),
        new KeyboardShortcut(ShortcutId.OpenInsights, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number5, "KeyboardShortcut_Action_OpenInsights"),
        new KeyboardShortcut(ShortcutId.OpenListening, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number0, "KeyboardShortcut_Action_OpenListening"),
        new KeyboardShortcut(ShortcutId.FocusSearch, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.F, "KeyboardShortcut_Action_FocusSearch"),
        new KeyboardShortcut(ShortcutId.Back, ShortcutCategory.Navigation, VirtualKeyModifiers.None, VirtualKey.Escape, "KeyboardShortcut_Action_Back"),

        new KeyboardShortcut(ShortcutId.ToggleFullScreen, ShortcutCategory.Modes, VirtualKeyModifiers.None, VirtualKey.F11, "KeyboardShortcut_Action_ToggleFullScreen"),
        new KeyboardShortcut(ShortcutId.ToggleCompact, ShortcutCategory.Modes, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, VirtualKey.M, "KeyboardShortcut_Action_ToggleCompact"),

        new KeyboardShortcut(ShortcutId.Help, ShortcutCategory.Help, VirtualKeyModifiers.None, VirtualKey.F1, "KeyboardShortcut_Action_Help"),
    };

    public static IReadOnlyList<KeyboardShortcut> All => _shortcuts;

    public static KeyboardShortcut ById(ShortcutId id)
    {
        for (int i = 0; i < _shortcuts.Count; i++)
        {
            if (_shortcuts[i].Id == id)
                return _shortcuts[i];
        }

        throw new KeyNotFoundException($"Shortcut not found: {id}");
    }

    public static IEnumerable<KeyboardShortcut> ByCategory(ShortcutCategory category)
    {
        return _shortcuts.Where(s => s.Category == category);
    }
}
