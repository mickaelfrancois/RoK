using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Tracks;
using Rok.Mapping;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Artist.Services;

public class ArtistDataLoader(IMediator mediator, IAlbumViewModelFactory albumViewModelFactory, ITrackViewModelFactory trackViewModelFactory, ILogger<ArtistDataLoader> logger)
{
    public async Task<ArtistDto?> LoadArtistAsync(long artistId)
    {
        Result<ArtistDto> resultArtist = await mediator.SendMessageAsync(new GetArtistByIdQuery(artistId));

        if (resultArtist.IsSuccess)
            return resultArtist.Value!;

        logger.LogError("Failed to load artist {ArtistId}", artistId);
        return null;
    }

    public async Task<List<AlbumViewModel>> LoadAlbumsAsync(long artistId)
    {
        IEnumerable<AlbumDto> albums = await mediator.SendMessageAsync(new GetAlbumsByArtistIdQuery(artistId));
        return AlbumViewModelMap.CreateViewModels(albums.ToList(), albumViewModelFactory);
    }

    public async Task<List<TrackViewModel>> LoadTracksAsync(long artistId)
    {
        IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetTracksByArtistIdQuery(artistId));
        return TrackViewModelMap.CreateViewModels(tracks.ToList(), trackViewModelFactory);
    }

    public async Task<ArtistDto?> ReloadArtistAsync(long artistId)
    {
        Result<ArtistDto> artistResult = await mediator.SendMessageAsync(new GetArtistByIdQuery(artistId));
        return artistResult.IsSuccess ? artistResult.Value : null;
    }
}