using Rok.Application.Features.Albums.Command;
using Rok.Application.Features.Artists.Command;
using Rok.Application.Features.Genres.Command;
using Rok.Application.Features.ListeningEvents.Command;
using Rok.Application.Features.Tracks.Command;

namespace Rok.ViewModels.Player.Services;

public class PlayerListenTracker(IMediator mediator)
{
    private readonly HashSet<long> _artistUpdatedCache = [];
    private readonly HashSet<long> _albumUpdatedCache = [];
    private readonly HashSet<long> _trackUpdatedCache = [];
    private readonly HashSet<long> _genreUpdatedCache = [];

    public void ClearCache()
    {
        _artistUpdatedCache.Clear();
        _albumUpdatedCache.Clear();
        _trackUpdatedCache.Clear();
        _genreUpdatedCache.Clear();
    }

    public async Task UpdateTrackListenAsync(long trackId)
    {
        if (_trackUpdatedCache.Contains(trackId))
            return;

        await mediator.SendMessageAsync(new UpdateTrackLastListenCommand(trackId));
        _trackUpdatedCache.Add(trackId);
    }

    public async Task UpdateArtistListenAsync(long artistId)
    {
        if (_artistUpdatedCache.Contains(artistId))
            return;

        await mediator.SendMessageAsync(new UpdateArtistLastListenCommand(artistId));
        _artistUpdatedCache.Add(artistId);
    }

    public async Task UpdateAlbumListenAsync(long albumId)
    {
        if (_albumUpdatedCache.Contains(albumId))
            return;

        await mediator.SendMessageAsync(new UpdateAlbumLastListenCommand(albumId));
        _albumUpdatedCache.Add(albumId);
    }

    public async Task UpdateGenreListenAsync(long genreId)
    {
        if (_genreUpdatedCache.Contains(genreId))
            return;

        await mediator.SendMessageAsync(new UpdateGenretLastListenCommand(genreId));
        _genreUpdatedCache.Add(genreId);
    }


    public Task UpdateListeningEventsAsync(long trackId, long? artistId, long? albumId, long? genreId, long durationPlayed, long durationTotal)
    {
        return mediator.SendMessageAsync(new CreateListeningEventCommand
        {
            TrackId = trackId,
            ArtistId = artistId,
            AlbumId = albumId,
            GenreId = genreId,
            DurationPlayed = durationPlayed,
            DurationTotal = durationTotal
        });
    }
}