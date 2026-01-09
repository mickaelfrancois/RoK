namespace Rok.Logic.ViewModels.Track.Services;

public class TrackNavigationService(NavigationService navigationService)
{
    public void NavigateToArtist(long? artistId)
    {
        if (artistId.HasValue)
            navigationService.NavigateToArtist(artistId.Value);
    }

    public void NavigateToAlbum(long? albumId)
    {
        if (albumId.HasValue)
            navigationService.NavigateToAlbum(albumId.Value);
    }

    public void NavigateToGenre(long? genreId)
    {
        if (genreId.HasValue)
            navigationService.NavigateToGenre(genreId.Value);
    }

    public void NavigateToTrack(long trackId)
    {
        if (trackId > 0)
            navigationService.NavigateToTrack(trackId);
    }
}