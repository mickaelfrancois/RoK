using CleanArch.DevKit.Guards;
using Rok.Application.Interfaces;
using Rok.Application.Player;
using Rok.Application.Player.Output;
using Rok.Shared.Enums;

namespace Rok.Application.Options;

public class AppOptions : IAppOptions
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public AppTheme Theme { get; set; } = AppTheme.System;

    public int AlbumRecentThresholdDays { get; set; } = 30;

    public int ArtistRecentThresholdDays { get; set; } = 30;

    public int WebApiPort { get; set; } = 5075;

    public bool EnableWebApi { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, the web API listens on every network interface instead of loopback only,
    /// which makes it reachable from other machines on the local network. Off by default: turning it
    /// on exposes playback control and the library listing to anyone on the same network.
    /// </summary>
    public bool WebApiAllowLan { get; set; }

    /// <summary>
    /// Absolute path of the folder holding the published web companion. Empty disables static file serving.
    /// </summary>
    public string WebAppRoot { get; set; } = string.Empty;

    public List<string> LibraryTokens { get; set; } = [];

    public bool CrossFade { get; set; } = true;

    public int CrossfadeDurationSeconds { get; set; } = CrossfadeDuration.DefaultSeconds;

    public bool MixMode { get; set; }

    public EReplayGainMode ReplayGainMode { get; set; } = EReplayGainMode.Auto;

    public double ReplayGainPreampDb { get; set; } = 0;

    public string OutputDeviceId { get; set; } = string.Empty;

    public EAudioOutputMode OutputMode { get; set; } = EAudioOutputMode.Shared;

    public ERepeatMode RepeatMode { get; set; } = ERepeatMode.Off;

    public bool ShuffleEnabled { get; set; }

    public bool IsGridView { get; set; } = true;

    public bool PauseOnCall { get; set; } = true;

    public bool HideArtistsWithoutAlbum { get; set; } = true;

    public bool RefreshLibraryAtStartup { get; set; } = true;

    public bool ImportTrackWithArtistGenre { get; set; } = true;

    public bool NovaApiEnabled { get; set; } = true;

    public bool TelemetryEnabled { get; set; } = true;

    public bool DiscordRichPresenceEnabled { get; set; } = true;

    public string? Language { get; set; } = null;

    public string CachePath { get; set; } = string.Empty;

    public string ArtistsGroupBy { get; set; } = string.Empty;

    public List<long> ArtistsFilterByGenresId { get; set; } = [];

    public List<string> ArtistsFilterBy { get; set; } = [];

    public List<string> ArtistsFilterByTags { get; set; } = [];

    public string AlbumsGroupBy { get; set; } = string.Empty;

    public List<long> AlbumsFilterByGenresId { get; set; } = [];

    public List<string> AlbumsFilterBy { get; set; } = [];

    public List<string> AlbumsFilterByTags { get; set; } = [];

    public string TracksGroupBy { get; set; } = string.Empty;

    public List<string> TracksFilterBy { get; set; } = [];

    public List<long> TracksFilterByGenresId { get; set; } = [];

    public List<string> TracksFilterByTags { get; set; } = [];

    public int SessionsCount { get; set; } = 0;

    public int TotalTracksListened { get; set; } = 0;

    public bool HasRated { get; set; } = false;

    public DateTimeOffset? ReviewLastPromptDate { get; set; } = null;

    public AppOptions()
    {
    }


    public void InitializeOptions(string applicationPath)
    {
        CachePath = Path.Combine(applicationPath, "Cache");
        Directory.CreateDirectory(CachePath);

        Id = Guid.NewGuid();
        ArtistsGroupBy = "ARTISTNAME";
        AlbumsGroupBy = "ALBUMNAME";
        TracksGroupBy = "ARTISTNAME";
    }


    public void SetCachePath(string path)
    {
        Guard.NotNullOrEmpty(path);

        CachePath = path;
    }

    public void CopyFrom(IAppOptions options)
    {
        Id = options.Id;

        LibraryTokens = options.LibraryTokens;

        CachePath = options.CachePath;

        Theme = options.Theme;
        AlbumRecentThresholdDays = options.AlbumRecentThresholdDays;
        ArtistRecentThresholdDays = options.ArtistRecentThresholdDays;
        Language = options.Language;
        RefreshLibraryAtStartup = options.RefreshLibraryAtStartup;
        HideArtistsWithoutAlbum = options.HideArtistsWithoutAlbum;
        ImportTrackWithArtistGenre = options.ImportTrackWithArtistGenre;
        TelemetryEnabled = options.TelemetryEnabled;
        NovaApiEnabled = options.NovaApiEnabled;
        DiscordRichPresenceEnabled = options.DiscordRichPresenceEnabled;
        PauseOnCall = options.PauseOnCall;
        CrossFade = options.CrossFade;
        CrossfadeDurationSeconds = options.CrossfadeDurationSeconds;
        MixMode = options.MixMode;
        IsGridView = options.IsGridView;
        ReplayGainMode = options.ReplayGainMode;
        ReplayGainPreampDb = options.ReplayGainPreampDb;
        OutputDeviceId = options.OutputDeviceId;
        OutputMode = options.OutputMode;
        RepeatMode = options.RepeatMode;
        ShuffleEnabled = options.ShuffleEnabled;
        WebApiPort = options.WebApiPort;
        EnableWebApi = options.EnableWebApi;
        WebApiAllowLan = options.WebApiAllowLan;
        WebAppRoot = options.WebAppRoot;

        ArtistsGroupBy = options.ArtistsGroupBy;
        ArtistsFilterBy = options.ArtistsFilterBy;
        ArtistsFilterByTags = options.ArtistsFilterByTags;
        ArtistsFilterByGenresId = options.ArtistsFilterByGenresId;

        AlbumsGroupBy = options.AlbumsGroupBy;
        AlbumsFilterBy = options.AlbumsFilterBy;
        AlbumsFilterByTags = options.AlbumsFilterByTags;
        AlbumsFilterByGenresId = options.AlbumsFilterByGenresId;

        TracksGroupBy = options.TracksGroupBy;
        TracksFilterBy = options.TracksFilterBy;
        TracksFilterByTags = options.TracksFilterByTags;
        TracksFilterByGenresId = options.TracksFilterByGenresId;

        SessionsCount = options.SessionsCount;
        TotalTracksListened = options.TotalTracksListened;
        HasRated = options.HasRated;
        ReviewLastPromptDate = options.ReviewLastPromptDate;
    }
}