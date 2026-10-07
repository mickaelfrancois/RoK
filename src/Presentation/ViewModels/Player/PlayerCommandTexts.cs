using Rok.Application.Player;
using Rok.Services.Accessibility;

namespace Rok.ViewModels.Player;

/// <summary>
/// Accessible names (labels) and tooltips of the player commands that do not change during the session.
/// Labels never contain a shortcut; tooltips do when the command has one.
/// </summary>
public sealed record PlayerCommandTexts
{
    public required string PreviousLabel { get; init; }

    public required string PreviousToolTip { get; init; }

    public required string NextLabel { get; init; }

    public required string NextToolTip { get; init; }

    public required string MuteLabel { get; init; }

    public required string MuteToolTip { get; init; }

    public required string VolumeLabel { get; init; }

    public required string QueueLabel { get; init; }

    public required string QueueToolTip { get; init; }

    public required string ShowCurrentPlaylistLabel { get; init; }

    public required string ShowLyricsLabel { get; init; }

    public required string ExitCompactLabel { get; init; }

    public required string ExitCompactToolTip { get; init; }

    public required string ExitFullScreenLabel { get; init; }

    public required string ExitFullScreenToolTip { get; init; }

    public required string CompactShortcutText { get; init; }

    public required string FullScreenShortcutText { get; init; }

    /// <summary>Builds the texts once, resolving each resource key through <paramref name="localize"/>.</summary>
    public static PlayerCommandTexts Create(Func<string, string> localize)
    {
        var previous = localize(PlayerCommandTextKeys.Previous);
        var next = localize(PlayerCommandTextKeys.Next);
        var mute = localize(PlayerCommandTextKeys.Mute);
        var queue = localize(PlayerCommandTextKeys.Queue);
        var exitCompact = localize(PlayerCommandTextKeys.ExitCompactMode);
        var exitFullScreen = localize(PlayerCommandTextKeys.ExitFullScreen);

        return new PlayerCommandTexts
        {
            PreviousLabel = previous,
            PreviousToolTip = KeyboardShortcutFormatter.WithShortcut(previous, ShortcutId.Previous),
            NextLabel = next,
            NextToolTip = KeyboardShortcutFormatter.WithShortcut(next, ShortcutId.Next),
            MuteLabel = mute,
            MuteToolTip = KeyboardShortcutFormatter.WithShortcut(mute, ShortcutId.Mute),
            VolumeLabel = localize(PlayerCommandTextKeys.Volume),
            QueueLabel = queue,
            QueueToolTip = KeyboardShortcutFormatter.WithShortcut(queue, ShortcutId.OpenListening),
            ShowCurrentPlaylistLabel = localize(PlayerCommandTextKeys.ShowCurrentPlaylist),
            ShowLyricsLabel = localize(PlayerCommandTextKeys.ShowLyrics),
            ExitCompactLabel = exitCompact,
            ExitCompactToolTip = KeyboardShortcutFormatter.WithShortcut(exitCompact, ShortcutId.ToggleCompact),
            ExitFullScreenLabel = exitFullScreen,
            ExitFullScreenToolTip = KeyboardShortcutFormatter.WithShortcut(exitFullScreen, ShortcutId.ToggleFullScreen),
            CompactShortcutText = KeyboardShortcutFormatter.Format(KeyboardShortcutCatalog.ById(ShortcutId.ToggleCompact)),
            FullScreenShortcutText = KeyboardShortcutFormatter.Format(KeyboardShortcutCatalog.ById(ShortcutId.ToggleFullScreen))
        };
    }

    /// <summary>Gets the play/pause accessible name for the given state (no shortcut).</summary>
    public static string PlayPauseLabel(EPlaybackState state, Func<string, string> localize)
    {
        return localize(PlayerCommandTextKeys.PlayPause(state));
    }

    /// <summary>Gets the play/pause tooltip for the given state, with the Space shortcut.</summary>
    public static string PlayPauseToolTip(EPlaybackState state, Func<string, string> localize)
    {
        return KeyboardShortcutFormatter.WithShortcut(PlayPauseLabel(state, localize), ShortcutId.PlayPause);
    }
}