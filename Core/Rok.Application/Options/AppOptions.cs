using System.ComponentModel;
using System.Runtime.CompilerServices;
using MiF.Guard;
using Rok.Application.Interfaces;
using Rok.Shared.Enums;

namespace Rok.Application.Options;

public class AppOptions : INotifyPropertyChanged, IAppOptions
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private Guid _id = Guid.NewGuid();
    public Guid Id { get => _id; set => SetProperty(ref _id, value); }

    private AppTheme _theme = AppTheme.System;
    public AppTheme Theme { get => _theme; set => SetProperty(ref _theme, value); }

    private List<string> _libraryTokens = [];
    public List<string> LibraryTokens { get => _libraryTokens; set => SetProperty(ref _libraryTokens, value); }

    private bool _crossFade = true;
    public bool CrossFade { get => _crossFade; set => SetProperty(ref _crossFade, value); }

    private bool _isGridView = true;
    public bool IsGridView { get => _isGridView; set => SetProperty(ref _isGridView, value); }

    private bool _hideArtistsWithoutAlbum = true;
    public bool HideArtistsWithoutAlbum { get => _hideArtistsWithoutAlbum; set => SetProperty(ref _hideArtistsWithoutAlbum, value); }

    private bool _refreshLibraryAtStartup = true;
    public bool RefreshLibraryAtStartup { get => _refreshLibraryAtStartup; set => SetProperty(ref _refreshLibraryAtStartup, value); }

    private bool _importTrackWithArtistGenre = true;
    public bool ImportTrackWithArtistGenre { get => _importTrackWithArtistGenre; set => SetProperty(ref _importTrackWithArtistGenre, value); }

    private bool _novaApiEnabled = true;
    public bool NovaApiEnabled { get => _novaApiEnabled; set => SetProperty(ref _novaApiEnabled, value); }

    private bool _telemetryEnabled = true;
    public bool TelemetryEnabled { get => _telemetryEnabled; set => SetProperty(ref _telemetryEnabled, value); }

    private bool _discordRichPresenceEnabled = true;
    public bool DiscordRichPresenceEnabled { get => _discordRichPresenceEnabled; set => SetProperty(ref _discordRichPresenceEnabled, value); }

    private string _cachePath = string.Empty;
    public string CachePath { get => _cachePath; set => SetProperty(ref _cachePath, value); }

    private string _artistsGroupBy = string.Empty;
    public string ArtistsGroupBy { get => _artistsGroupBy; set => SetProperty(ref _artistsGroupBy, value); }

    private List<long> _artistsFilterByGenresId = [];
    public List<long> ArtistsFilterByGenresId { get => _artistsFilterByGenresId; set => SetProperty(ref _artistsFilterByGenresId, value); }

    private List<string> _artistsFilterBy = [];
    public List<string> ArtistsFilterBy { get => _artistsFilterBy; set => SetProperty(ref _artistsFilterBy, value); }

    private string _albumsGroupBy = string.Empty;
    public string AlbumsGroupBy { get => _albumsGroupBy; set => SetProperty(ref _albumsGroupBy, value); }

    private List<long> _albumsFilterByGenresId = [];
    public List<long> AlbumsFilterByGenresId { get => _albumsFilterByGenresId; set => SetProperty(ref _albumsFilterByGenresId, value); }

    private List<string> _albumsFilterBy = [];
    public List<string> AlbumsFilterBy { get => _albumsFilterBy; set => SetProperty(ref _albumsFilterBy, value); }

    private string _tracksGroupBy = string.Empty;
    public string TracksGroupBy { get => _tracksGroupBy; set => SetProperty(ref _tracksGroupBy, value); }

    private List<string> _tracksFilterBy = [];
    public List<string> TracksFilterBy { get => _tracksFilterBy; set => SetProperty(ref _tracksFilterBy, value); }

    private List<long> _tracksFilterByGenresId = [];
    public List<long> TracksFilterByGenresId { get => _tracksFilterByGenresId; set => SetProperty(ref _tracksFilterByGenresId, value); }

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
