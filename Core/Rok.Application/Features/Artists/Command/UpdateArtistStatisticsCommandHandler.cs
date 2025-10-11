using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;

public class UpdateArtistStatisticsCommand(long id) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;

    public int TrackCount { get; set; }

    public long TotalDurationSeconds { get; set; }

    public int AlbumCount { get; set; }

    public int BestOfCount { get; set; }

    public int LiveCount { get; set; }

    public int CompilationCount { get; set; }

    public int? YearMini { get; set; }

    public int? YearMaxi { get; set; }
}


public class UpdateArtistStatisticsCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<UpdateArtistStatisticsCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateArtistStatisticsCommand message, CancellationToken cancellationToken)
    {
        if (message.YearMini.HasValue && message.YearMini.Value == 0)
            message.YearMini = null;
        if (message.YearMaxi.HasValue && message.YearMaxi.Value == 0)
            message.YearMaxi = null;

        bool result = await _artistRepository.UpdateStatisticsAsync(message.Id, message.TrackCount, message.TotalDurationSeconds, message.AlbumCount, message.BestOfCount, message.LiveCount, message.CompilationCount, message.YearMini, message.YearMaxi);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update artist statistics.");
    }
}