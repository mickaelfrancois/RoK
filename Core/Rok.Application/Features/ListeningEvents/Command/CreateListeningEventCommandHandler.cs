using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.ListeningEvents.Command;

public class CreateListeningEventCommand : ICommand<Result<long>>
{
    [Required]
    public long TrackId { get; set; }

    public long? ArtistId { get; set; }

    public long? AlbumId { get; set; }

    public long? GenreId { get; set; }

    public long DurationPlayed { get; set; }

    public long DurationTotal { get; set; }
}


public class CreateListeningEventCommandHandler(IListeningEventRepository _listeningEventRepository) : ICommandHandler<CreateListeningEventCommand, Result<long>>
{
    public async Task<Result<long>> HandleAsync(CreateListeningEventCommand message, CancellationToken cancellationToken)
    {
        double completionRate = (double)message.DurationPlayed / message.DurationTotal;
        bool fastChange = message.DurationPlayed < 30 && completionRate < 0.2;
        if (fastChange)
            return Result<long>.Success(0);

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
            return Result<long>.Success(id);
        else
            return Result<long>.Fail("Failed to create listening event.");
    }
}
