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


public class CreateListeningEventRequestHandler(IListeningEventRepository _listeningEventRepository) : IRequestHandler<CreateListeningEventRequest, Result<long>>
{
    public async Task<Result<long>> Handle(CreateListeningEventRequest message, CancellationToken cancellationToken)
    {
        double completionRate = (double)message.DurationPlayed / message.DurationTotal;
        bool fastChange = message.DurationPlayed < 30 && completionRate < 0.2;
        if (fastChange)
            return Result<long>.Ok(0);

        bool wasSkipped = message.DurationPlayed >= 30 && completionRate < 0.2;

        ListeningEventEntity entity = new()
        {
            TrackId = message.TrackId,
            ArtistId = message.ArtistId,
            AlbumId = message.AlbumId,
            GenreId = message.GenreId,
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
}