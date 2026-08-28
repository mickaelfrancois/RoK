using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.ListeningEvents;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Album.Services;

public class AlbumDataLoader(IMediator mediator, ITrackViewModelFactory trackViewModelFactory, ILogger<AlbumDataLoader> logger)
{
    public async Task<AlbumDto?> LoadAlbumAsync(long albumId)
    {
        Result<AlbumDto> resultAlbum = await mediator.Send(new GetAlbumByIdRequest(albumId));

        if (resultAlbum.IsSuccess)
            return resultAlbum.Value;

        logger.LogError("Failed to load album {AlbumId}", albumId);
        return null;
    }

    public async Task<List<TrackViewModel>> LoadTracksAsync(long albumId)
    {
        Result<IEnumerable<TrackDto>> result = await mediator.Send(new GetTracksByAlbumIdRequest(albumId));

        if (result.IsFailure)
        {
            logger.LogError("Failed to load tracks for album {AlbumId}", albumId);
            return [];
        }

        return TrackViewModelMap.CreateViewModels(result.Value.ToList(), trackViewModelFactory);
    }

    public async Task<AlbumDto?> ReloadAlbumAsync(long albumId)
    {
        Result<AlbumDto> albumResult = await mediator.Send(new GetAlbumByIdRequest(albumId));
        return albumResult.IsSuccess ? albumResult.Value : null;
    }

    public Task<ListeningStatsDto> LoadListeningStatsAsync(long albumId)
    {
        return mediator.Send(new GetAlbumListeningStatsRequest(albumId));
    }
}