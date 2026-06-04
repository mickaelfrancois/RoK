using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.ListeningEvents;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Artist.Services;

public class ArtistDataLoader(IMediator mediator, IAlbumViewModelFactory albumViewModelFactory, ITrackViewModelFactory trackViewModelFactory, ILogger<ArtistDataLoader> logger)
{
    public async Task<ArtistDto?> LoadArtistAsync(long artistId)
    {
        Result<ArtistDto> resultArtist = await mediator.Send(new GetArtistByIdRequest(artistId));

        if (resultArtist.IsSuccess)
            return resultArtist.Value;

        logger.LogError("Failed to load artist {ArtistId}", artistId);
        return null;
    }

    public async Task<List<AlbumViewModel>> LoadAlbumsAsync(long artistId)
    {
        IEnumerable<AlbumDto> albums = await mediator.Send(new GetAlbumsByArtistIdRequest(artistId));
        return AlbumViewModelMap.CreateViewModels(albums.ToList(), albumViewModelFactory);
    }

    public async Task<List<TrackViewModel>> LoadTracksAsync(long artistId)
    {
        IEnumerable<TrackDto> tracks = await mediator.Send(new GetTracksByArtistIdRequest(artistId));
        return TrackViewModelMap.CreateViewModels(tracks.ToList(), trackViewModelFactory);
    }

    public async Task<ArtistDto?> ReloadArtistAsync(long artistId)
    {
        Result<ArtistDto> artistResult = await mediator.Send(new GetArtistByIdRequest(artistId));
        return artistResult.IsSuccess ? artistResult.Value : null;
    }

    public Task<ListeningStatsDto> LoadListeningStatsAsync(long artistId)
    {
        return mediator.Send(new GetArtistListeningStatsRequest(artistId));
    }
}