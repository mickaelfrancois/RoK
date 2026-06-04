using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Features.ListeningEvents.Requests;
using Rok.Application.Features.Tracks.Requests;

namespace Rok.ViewModels.Player.Services;

public class PlayerListenTracker(IMediator mediator)
{
    private readonly HashSet<long> _artistUpdatedCache = [];
    private readonly HashSet<long> _albumUpdatedCache = [];
    private readonly HashSet<long> _trackUpdatedCache = [];
    private readonly HashSet<long> _genreUpdatedCache = [];

    /// <summary>
    /// Number of tracks listened during the session (at least half of the track played).
    /// </summary>
    public int SessionListenedCount { get; private set; }

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

        await mediator.Send(new UpdateTrackLastListenRequest(trackId));
        _trackUpdatedCache.Add(trackId);
    }

    public async Task UpdateArtistListenAsync(long artistId)
    {
        if (_artistUpdatedCache.Contains(artistId))
            return;

        await mediator.Send(new UpdateArtistLastListenRequest(artistId));
        _artistUpdatedCache.Add(artistId);
    }

    public async Task UpdateAlbumListenAsync(long albumId)
    {
        if (_albumUpdatedCache.Contains(albumId))
            return;

        await mediator.Send(new UpdateAlbumLastListenRequest(albumId));
        _albumUpdatedCache.Add(albumId);
    }

    public async Task UpdateGenreListenAsync(long genreId)
    {
        if (_genreUpdatedCache.Contains(genreId))
            return;

        await mediator.Send(new UpdateGenretLastListenRequest(genreId));
        _genreUpdatedCache.Add(genreId);
    }


    public Task UpdateListeningEventsAsync(long trackId, long? artistId, long? albumId, long? genreId, long durationPlayed, long durationTotal)
    {
        if (durationTotal > 0 && durationPlayed * 2 >= durationTotal)
            SessionListenedCount++;

        return mediator.Send(new CreateListeningEventRequest
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