using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Pages;
using Rok.Services.Diagnostics;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Genre;
using Rok.ViewModels.Playlist;
using Rok.ViewModels.Search;
using Rok.ViewModels.Track;

namespace Rok.Services;

public class NavigationService(ITelemetryClient telemetryClient, NavigationTrail trail, ICrashBreadcrumbs breadcrumbs)
{
    private Frame _mainFrame = default!;

    public NavigationService(ITelemetryClient telemetryClient)
        : this(telemetryClient, new NavigationTrail(TimeProvider.System), new CrashBreadcrumbs(TimeProvider.System))
    {
    }

    public Frame MainFrame
    {
        get => _mainFrame;
        set
        {
            if (_mainFrame is not null)
            {
                _mainFrame.Navigating -= OnFrameNavigating;
                _mainFrame.Navigated -= OnFrameNavigated;
                _mainFrame.NavigationFailed -= OnFrameNavigationFailed;
            }

            _mainFrame = value;

            if (_mainFrame is not null)
            {
                _mainFrame.Navigating += OnFrameNavigating;
                _mainFrame.Navigated += OnFrameNavigated;
                _mainFrame.NavigationFailed += OnFrameNavigationFailed;
            }
        }
    }

    // Tracked for crash telemetry: a bare MeasureOverride COMException carries no app frame,
    // so the navigation phase and the current/previous page are the only clues to which
    // screen triggered it.
    public NavigationTrail Trail => trail;

    public string? CurrentPageName => trail.Snapshot().CurrentPage;

    public string? PreviousPageName => trail.Snapshot().PreviousPage;

    private void OnFrameNavigating(object sender, NavigatingCancelEventArgs e)
    {
        string target = e.SourcePageType?.Name ?? "unknown";

        trail.OnNavigating(target);
        breadcrumbs.Add("nav.navigating", target);
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        string? page = e.SourcePageType?.Name ?? e.Content?.GetType().Name;

        trail.OnNavigated(page);
        breadcrumbs.Add("nav.navigated", page ?? "unknown");

        if (page is not null && e.Content is FrameworkElement content)
            ReportLoadedOnce(content, page);
    }

    private void OnFrameNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        string target = e.SourcePageType?.Name ?? "unknown";

        trail.OnFailed(target);
        breadcrumbs.Add("nav.failed", target);
    }

    private void ReportLoadedOnce(FrameworkElement content, string page)
    {
        void OnLoaded(object sender, RoutedEventArgs args)
        {
            content.Loaded -= OnLoaded;
            trail.OnLoaded(page);
            breadcrumbs.Add("nav.loaded", page);
        }

        content.Loaded += OnLoaded;
    }

    // Pages whose rebuild on a repeated request only costs: ListeningPage rebuilds the whole queue.
    // Other list pages keep their "click again to reload" behaviour.
    private static readonly HashSet<string> _pagesWithoutRenavigation = [nameof(ListeningPage)];

    private bool ShouldSkipRedundantNavigation(string pageName, bool hasParameter)
    {
        if (!_pagesWithoutRenavigation.Contains(pageName) || !trail.IsRedundant(pageName, hasParameter))
            return false;

        breadcrumbs.Add("nav.skip", pageName);
        return true;
    }

    public void NavigateTo(Type pageType)
    {
        NavigateTo(pageType, null);
    }

    public void NavigateTo(Type pageType, object? parameter)
    {
        if (ShouldSkipRedundantNavigation(pageType.Name, parameter is not null))
            return;

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
        if (ShouldSkipRedundantNavigation(nameof(ListeningPage), hasParameter: false))
            return;

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