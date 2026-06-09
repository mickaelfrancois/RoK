using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Pages;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Genre;
using Rok.ViewModels.Playlist;
using Rok.ViewModels.Search;
using Rok.ViewModels.Track;

namespace Rok.Services;

public class NavigationService(ITelemetryClient telemetryClient)
{
    private Frame _mainFrame = default!;

    public Frame MainFrame
    {
        get => _mainFrame;
        set
        {
            if (_mainFrame is not null)
                _mainFrame.Navigated -= OnFrameNavigated;

            _mainFrame = value;

            if (_mainFrame is not null)
                _mainFrame.Navigated += OnFrameNavigated;
        }
    }

    // Tracked for crash telemetry: a bare MeasureOverride COMException carries no app frame,
    // so the current/previous page is the only clue to which screen triggered it.
    public string? CurrentPageName { get; private set; }

    public string? PreviousPageName { get; private set; }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        PreviousPageName = CurrentPageName;
        CurrentPageName = e.SourcePageType?.Name ?? e.Content?.GetType().Name;
    }

    public void NavigateTo(Type pageType)
    {
        NavigateTo(pageType, null);
    }

    public void NavigateTo(Type pageType, object? parameter)
    {
        _ = telemetryClient.CaptureScreenAsync(pageType.Name);
        MainFrame.Navigate(pageType, parameter);
    }

    public void NavigateToArtist(long artistId)
    {
        Guard.NotNegativeOrZero(artistId);
        _ = telemetryClient.CaptureScreenAsync("ArtistPage");
        MainFrame.Navigate(typeof(ArtistPage), new ArtistOpenArgs(artistId));
    }

    public void NavigateToAlbums()
    {
        _ = telemetryClient.CaptureScreenAsync("AlbumsPage");
        MainFrame.Navigate(typeof(AlbumsPage));
    }

    public void NavigateToAlbum(long albumId)
    {
        Guard.NotNegativeOrZero(albumId);
        _ = telemetryClient.CaptureScreenAsync("AlbumPage");
        MainFrame.Navigate(typeof(AlbumPage), new AlbumOpenArgs(albumId));
    }

    public void NavigateToGenre(long genreId)
    {
        Guard.NotNegativeOrZero(genreId);
        _ = telemetryClient.CaptureScreenAsync("GenrePage");
        MainFrame.Navigate(typeof(GenrePage), new GenreOpenArgs(genreId));
    }

    public void NavigateToTrack(long trackId)
    {
        Guard.NotNegativeOrZero(trackId);
        _ = telemetryClient.CaptureScreenAsync("TrackPage");
        MainFrame.Navigate(typeof(TrackPage), new TrackOpenArgs(trackId));
    }

    public void NavigateToSmartPlaylist(long playlistId)
    {
        _ = telemetryClient.CaptureScreenAsync("SmartPlaylistPage");
        MainFrame.Navigate(typeof(SmartPlaylistPage), new PlaylistOpenArgs(playlistId));
    }

    public void NavigateToPlaylist(long playlistId)
    {
        _ = telemetryClient.CaptureScreenAsync("PlaylistPage");
        MainFrame.Navigate(typeof(PlaylistPage), new PlaylistOpenArgs(playlistId));
    }

    public void NavigateToPlaylists()
    {
        _ = telemetryClient.CaptureScreenAsync("PlaylistsPage");
        MainFrame.Navigate(typeof(PlaylistsPage));
    }

    public void NavigateToListening()
    {
        _ = telemetryClient.CaptureScreenAsync("ListeningPage");
        MainFrame.Navigate(typeof(ListeningPage));
    }

    public void NavigateToSearch(SearchOpenArgs args)
    {
        Guard.NotNull(args);
        _ = telemetryClient.CaptureScreenAsync("SearchPage");
        MainFrame.Navigate(typeof(SearchPage), args);
    }

    public void RemoveLastEntry()
    {
        if (MainFrame.CanGoBack)
            MainFrame.BackStack.RemoveAt(MainFrame.BackStack.Count - 1);
    }
}