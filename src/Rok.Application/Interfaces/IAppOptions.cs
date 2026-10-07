using Rok.Application.Player;
using Rok.Application.Player.Output;
using Rok.Shared.Enums;

namespace Rok.Application.Interfaces;

public interface IAppOptions
{
    Guid Id { get; set; }

    AppTheme Theme { get; set; }

    int AlbumRecentThresholdDays { get; set; }

    int ArtistRecentThresholdDays { get; set; }

    int WebApiPort { get; set; }

    bool EnableWebApi { get; set; }

    bool WebApiAllowLan { get; set; }

    string WebAppRoot { get; set; }

    List<string> LibraryTokens { get; set; }

    bool CrossFade { get; set; }

    /// <summary>Crossfade duration in seconds, within <see cref="Player.CrossfadeDuration"/> bounds.</summary>
    int CrossfadeDurationSeconds { get; set; }

    /// <summary>Smart mix: the crossfade follows the real end of the track and the real start of the next one; <see cref="CrossfadeDurationSeconds"/> becomes a maximum.</summary>
    bool MixMode { get; set; }

    EReplayGainMode ReplayGainMode { get; set; }

    double ReplayGainPreampDb { get; set; }

    /// <summary>Identifier of the chosen output device; empty to follow the Windows default device.</summary>
    string OutputDeviceId { get; set; }

    EAudioOutputMode OutputMode { get; set; }

    /// <summary>Repeat mode of the player, restored at startup.</summary>
    ERepeatMode RepeatMode { get; set; }

    /// <summary>Whether the reversible shuffle is enabled, restored at startup.</summary>
    bool ShuffleEnabled { get; set; }

    bool IsGridView { get; set; }

    bool PauseOnCall { get; set; }

    bool HideArtistsWithoutAlbum { get; set; }

    bool RefreshLibraryAtStartup { get; set; }

    bool ImportTrackWithArtistGenre { get; set; }

    bool NovaApiEnabled { get; set; }

    bool TelemetryEnabled { get; set; }

    bool DiscordRichPresenceEnabled { get; set; }

    string? Language { get; set; }

    string CachePath { get; set; }

    string ArtistsGroupBy { get; set; }

    List<string> ArtistsFilterBy { get; set; }

    List<string> ArtistsFilterByTags { get; set; }

    List<long> ArtistsFilterByGenresId { get; set; }

    string AlbumsGroupBy { get; set; }

    List<string> AlbumsFilterBy { get; set; }

    List<long> AlbumsFilterByGenresId { get; set; }

    List<string> AlbumsFilterByTags { get; set; }

    string TracksGroupBy { get; set; }

    List<string> TracksFilterBy { get; set; }

    List<string> TracksFilterByTags { get; set; }

    List<long> TracksFilterByGenresId { get; set; }

    int SessionsCount { get; set; }

    int TotalTracksListened { get; set; }

    bool HasRated { get; set; }

    DateTimeOffset? ReviewLastPromptDate { get; set; }

    void SetCachePath(string path);

    void CopyFrom(IAppOptions options);

    void InitializeOptions(string applicationPath);
}