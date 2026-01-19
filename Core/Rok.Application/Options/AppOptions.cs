using MiF.Guard;
using Rok.Application.Interfaces;
using Rok.Shared.Enums;

namespace Rok.Application.Options;

public class AppOptions : BaseNotifyPropertyChanged, IAppOptions
{
    public Guid Id { get => field; set => SetProperty(ref field, value); } = Guid.NewGuid();

    public AppTheme Theme { get => field; set => SetProperty(ref field, value); } = AppTheme.System;

    public List<string> LibraryTokens { get => field; set => SetProperty(ref field, value); } = new List<string>();

    public bool CrossFade { get => field; set => SetProperty(ref field, value); } = true;

    public bool IsGridView { get => field; set => SetProperty(ref field, value); } = true;

    public bool HideArtistsWithoutAlbum { get => field; set => SetProperty(ref field, value); } = true;

    public bool RefreshLibraryAtStartup { get => field; set => SetProperty(ref field, value); } = true;

    public bool ImportTrackWithArtistGenre { get => field; set => SetProperty(ref field, value); } = true;

    public bool NovaApiEnabled { get => field; set => SetProperty(ref field, value); } = true;

    public bool TelemetryEnabled { get => field; set => SetProperty(ref field, value); } = true;

    public bool DiscordRichPresenceEnabled { get => field; set => SetProperty(ref field, value); } = true;

    public string CachePath { get => field; set => SetProperty(ref field, value); } = string.Empty;

    public string ArtistsGroupBy { get => field; set => SetProperty(ref field, value); } = string.Empty;

    public List<long> ArtistsFilterByGenresId { get => field; set => SetProperty(ref field, value); } = new List<long>();

    public List<string> ArtistsFilterBy { get => field; set => SetProperty(ref field, value); } = new List<string>();

    public string AlbumsGroupBy { get => field; set => SetProperty(ref field, value); } = string.Empty;

    public List<long> AlbumsFilterByGenresId { get => field; set => SetProperty(ref field, value); } = new List<long>();

    public List<string> AlbumsFilterBy { get => field; set => SetProperty(ref field, value); } = new List<string>();

    public string TracksGroupBy { get => field; set => SetProperty(ref field, value); } = string.Empty;

    public List<string> TracksFilterBy { get => field; set => SetProperty(ref field, value); } = new List<string>();

    public List<long> TracksFilterByGenresId { get => field; set => SetProperty(ref field, value); } = new List<long>();

    public AppOptions() { }

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