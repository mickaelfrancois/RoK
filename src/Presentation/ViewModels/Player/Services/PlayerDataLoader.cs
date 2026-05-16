using Rok.Application.Features.Albums.Requests;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists.Interfaces;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Player.Services;

public class PlayerDataLoader(IMediator mediator, IArtistViewModelFactory artistViewModelFactory, IAlbumViewModelFactory albumViewModelFactory, ITrackViewModelFactory trackViewModelFactory, ILogger<PlayerDataLoader> logger)
{
    public async Task<AlbumViewModel?> GetAlbumByIdAsync(long albumId)
    {
        Result<AlbumDto> albumResult = await mediator.Send(new GetAlbumByIdRequest(albumId));
        if (albumResult.IsFailure)
        {
            logger.LogError("Failed to get album by ID {AlbumId}: {ErrorMessage}", albumId, albumResult.Errors[0]);
            return null;
        }

        AlbumViewModel albumViewModel = albumViewModelFactory.Create();
        albumViewModel.SetData(albumResult.Value);

        return albumViewModel;
    }

    public async Task<ArtistViewModel?> GetArtistByIdAsync(long artistId)
    {
        Result<ArtistDto> artistResult = await mediator.Send(new GetArtistByIdRequest(artistId));
        if (artistResult.IsFailure)
        {
            logger.LogError("Failed to get artist by ID {ArtistId}: {ErrorMessage}", artistId, artistResult.Errors[0]);
            return null;
        }

        ArtistViewModel artistViewModel = artistViewModelFactory.Create();
        artistViewModel.SetData(artistResult.Value);

        return artistViewModel;
    }

    public TrackViewModel CreateTrackViewModel(TrackDto track)
    {
        TrackViewModel trackViewModel = trackViewModelFactory.Create();
        trackViewModel.SetData(track);
        return trackViewModel;
    }
}