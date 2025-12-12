using Rok.Application.Features.Albums.Query;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Player.Services;

public class PlayerDataLoader(IMediator mediator, ILogger<PlayerDataLoader> logger)
{
    public async Task<AlbumViewModel?> GetAlbumByIdAsync(long albumId)
    {
        Result<AlbumDto> albumResult = await mediator.SendMessageAsync(new GetAlbumByIdQuery(albumId));
        if (albumResult.IsError)
        {
            logger.LogError("Failed to get album by ID {AlbumId}: {ErrorMessage}", albumId, albumResult.Error);
            return null;
        }

        AlbumViewModel albumViewModel = App.ServiceProvider.GetRequiredService<AlbumViewModel>();
        albumViewModel.SetData(albumResult.Value!);

        return albumViewModel;
    }

    public async Task<ArtistViewModel?> GetArtistByIdAsync(long artistId)
    {
        Result<ArtistDto> artistResult = await mediator.SendMessageAsync(new GetArtistByIdQuery(artistId));
        if (artistResult.IsError)
        {
            logger.LogError("Failed to get artist by ID {ArtistId}: {ErrorMessage}", artistId, artistResult.Error);
            return null;
        }

        ArtistViewModel artistViewModel = App.ServiceProvider.GetRequiredService<ArtistViewModel>();
        artistViewModel.SetData(artistResult.Value!);

        return artistViewModel;
    }

    public TrackViewModel CreateTrackViewModel(TrackDto track)
    {
        TrackViewModel trackViewModel = App.ServiceProvider.GetRequiredService<TrackViewModel>();
        trackViewModel.SetData(track);
        return trackViewModel;
    }
}