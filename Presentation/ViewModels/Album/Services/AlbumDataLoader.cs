using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Logic.ViewModels.Tracks;
using Rok.Mapping;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Album.Services;

public class AlbumDataLoader(IMediator mediator, ITrackViewModelFactory trackViewModelFactory, ILogger<AlbumDataLoader> logger)
{
    public async Task<AlbumDto?> LoadAlbumAsync(long albumId)
    {
        Result<AlbumDto> resultAlbum = await mediator.SendMessageAsync(new GetAlbumByIdQuery(albumId));

        if (resultAlbum.IsSuccess)
            return resultAlbum.Value!;

        logger.LogError("Failed to load album {AlbumId}", albumId);
        return null;
    }

    public async Task<List<TrackViewModel>> LoadTracksAsync(long albumId)
    {
        IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetTracksByAlbumIdQuery(albumId));
        return TrackViewModelMap.CreateViewModels(tracks.ToList(), trackViewModelFactory);
    }

    public async Task<AlbumDto?> ReloadAlbumAsync(long albumId)
    {
        Result<AlbumDto> albumResult = await mediator.SendMessageAsync(new GetAlbumByIdQuery(albumId));
        return albumResult.IsSuccess ? albumResult.Value : null;
    }
}