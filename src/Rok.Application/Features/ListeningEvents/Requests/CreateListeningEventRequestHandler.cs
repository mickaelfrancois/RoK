using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.ListeningEvents.Requests;

public class CreateListeningEventRequest : IRequest<Result<long>>
{
    public long TrackId { get; set; }

    public long? ArtistId { get; set; }

    public long? AlbumId { get; set; }

    public long? GenreId { get; set; }

    public long DurationPlayed { get; set; }

    public long DurationTotal { get; set; }
}

public sealed class CreateListeningEventRequestValidator : Validator<CreateListeningEventRequest>
{
    public CreateListeningEventRequestValidator() { Rule(x => x.TrackId).GreaterThan(0L); }
}


/// <summary>
/// Persists a listening event. ListeningEvents is the only table enforcing foreign keys, so the
/// identifiers carried by the request — read from the in-memory playback queue, which is never
/// refreshed — are not trusted: the track is re-read and every reference that no longer exists is
/// dropped rather than inserted.
/// </summary>
public class CreateListeningEventRequestHandler(IListeningEventRepository _listeningEventRepository,
                                                ITrackRepository _trackRepository,
                                                IArtistRepository _artistRepository,
                                                IAlbumRepository _albumRepository,
                                                IGenreRepository _genreRepository) : IRequestHandler<CreateListeningEventRequest, Result<long>>
{
    private const long MinimumListenSeconds = 30;

    private const double SkipCompletionRate = 0.2;

    public async Task<Result<long>> Handle(CreateListeningEventRequest message, CancellationToken cancellationToken)
    {
        bool hasKnownDuration = message.DurationTotal > 0;
        bool shortPlay = message.DurationPlayed < MinimumListenSeconds;

        // TagLib reports a zero duration for files it cannot read, and dividing by it yields NaN —
        // which compares false against every threshold and silently turns a two second play into a
        // full listen. Without a duration the played time alone decides.
        bool partialPlay = hasKnownDuration && (double)message.DurationPlayed / message.DurationTotal < SkipCompletionRate;

        bool fastChange = shortPlay && (partialPlay || !hasKnownDuration);
        if (fastChange)
            return Result<long>.Ok(0);

        bool wasSkipped = !shortPlay && partialPlay;

        TrackEntity? track = await _trackRepository.GetByIdAsync(message.TrackId);

        if (track is null)
            return Result<long>.Fail(new NotFoundError("track.not_found", $"Track {message.TrackId} was not found."));

        ListeningEventEntity entity = new()
        {
            TrackId = track.Id,
            ArtistId = await KeepWhenPresentAsync(track.ArtistId, _artistRepository),
            AlbumId = await KeepWhenPresentAsync(track.AlbumId, _albumRepository),
            GenreId = await KeepWhenPresentAsync(track.GenreId, _genreRepository),
            PlayedAt = DateTime.UtcNow,
            WasSkipped = wasSkipped,
            DurationPlayed = message.DurationPlayed,
            DurationTotal = message.DurationTotal
        };

        long id = await _listeningEventRepository.AddAsync(entity);

        if (id > 0)
            return Result<long>.Ok(id);
        else
            return Result<long>.Fail(new OperationError("listening_event.create_failed", "Failed to create listening event."));
    }

    private static async Task<long?> KeepWhenPresentAsync<TEntity>(long? id, IRepository<TEntity> repository) where TEntity : class
    {
        if (id is null or <= 0)
            return null;

        TEntity? entity = await repository.GetByIdAsync(id.Value);

        return entity is null ? null : id;
    }
}