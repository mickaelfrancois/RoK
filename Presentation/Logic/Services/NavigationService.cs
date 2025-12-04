using Microsoft.UI.Xaml.Controls;
using Rok.Logic.ViewModels.Album;
using Rok.Logic.ViewModels.Artist;
using Rok.Logic.ViewModels.Playlists;
using Rok.Logic.ViewModels.Search;
using Rok.Logic.ViewModels.Track;
using Rok.Pages;

namespace Rok.Logic.Services;

public class NavigationService(ITelemetryClient telemetryClient)
{
    public Frame MainFrame { set; get; } = default!;

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
        Guard.Against.NegativeOrZero(artistId);

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
        Guard.Against.NegativeOrZero(albumId);

        _ = telemetryClient.CaptureScreenAsync("AlbumPage");

        MainFrame.Navigate(typeof(AlbumPage), new AlbumOpenArgs(albumId));
    }


    public void NavigateToTrack(long trackId)
    {
        Guard.Against.NegativeOrZero(trackId);

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
        Guard.Against.Null(args);

        _ = telemetryClient.CaptureScreenAsync("SearchPage");

        MainFrame.Navigate(typeof(SearchPage), args);
    }

    public void RemoveLastEntry()
    {
        if (MainFrame.CanGoBack)
            MainFrame.BackStack.RemoveAt(MainFrame.BackStack.Count - 1);
    }
}
