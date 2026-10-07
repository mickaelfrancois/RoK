using Rok.Application.Player;

namespace Rok.ViewModels.Player;

/// <summary>Resource keys of the texts naming the player commands (accessible names and tooltips).</summary>
public static class PlayerCommandTextKeys
{
    public const string Previous = "KeyboardShortcut_Action_Previous";
    public const string Next = "KeyboardShortcut_Action_Next";
    public const string Mute = "KeyboardShortcut_Action_Mute";
    public const string Queue = "playerQueue";
    public const string ShowCurrentPlaylist = "playerShowCurrentPlaylist";
    public const string ShowLyrics = "playerShowLyrics";
    public const string ExitFullScreen = "playerExitFullScreen";
    public const string ExitCompactMode = "playerExitCompactMode";
    public const string Volume = "playerVolume";

    private const string Play = "playerPlay";
    private const string Pause = "playerPause";

    /// <summary>Gets the key of the play/pause command text: pause while playing, play otherwise.</summary>
    public static string PlayPause(EPlaybackState state) => state == EPlaybackState.Playing ? Pause : Play;

    /// <summary>Gets every key used by the player command texts.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Previous,
        Next,
        Mute,
        Queue,
        ShowCurrentPlaylist,
        ShowLyrics,
        ExitFullScreen,
        ExitCompactMode,
        Volume,
        Play,
        Pause
    ];
}