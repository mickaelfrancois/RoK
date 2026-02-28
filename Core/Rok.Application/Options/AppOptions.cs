using MiF.Guard;
using Rok.Application.Interfaces;
using Rok.Shared.Enums;

namespace Rok.Application.Options;

public class AppOptions : IAppOptions
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public AppTheme Theme { get; set; } = AppTheme.System;

    public int AlbumRecentThresholdDays { get; set; } = 30;

    public int ArtistRecentThresholdDays { get; set; } = 30;

    public List<string> LibraryTokens { get; set; } = [];

    public bool CrossFade { get; set; } = true;

    public bool IsGridView { get; set; } = true;

    public bool HideArtistsWithoutAlbum { get; set; } = true;

    public bool RefreshLibraryAtStartup { get; set; } = true;

    public bool ImportTrackWithArtistGenre { get; set; } = true;

    public bool NovaApiEnabled { get; set; } = true;

    public bool TelemetryEnabled { get; set; } = true;

    public bool DiscordRichPresenceEnabled { get; set; } = true;

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
        Guard.Against.NullOrEmpty(path);

        CachePath = path;
    }

    public void CopyFrom(IAppOptions options)
    {
        Id = options.Id;

        LibraryTokens = options.LibraryTokens;

        CachePath = options.CachePath;

        Theme = options.Theme;
        RefreshLibraryAtStartup = options.RefreshLibraryAtStartup;
        HideArtistsWithoutAlbum = options.HideArtistsWithoutAlbum;
        TelemetryEnabled = options.TelemetryEnabled;
        NovaApiEnabled = options.NovaApiEnabled;

        ArtistsGroupBy = options.ArtistsGroupBy;
        ArtistsFilterBy = options.ArtistsFilterBy;

        AlbumsGroupBy = options.AlbumsGroupBy;
        AlbumsFilterBy = options.AlbumsFilterBy;

        TracksGroupBy = options.TracksGroupBy;
        TracksFilterBy = options.TracksFilterBy;
    }
}
