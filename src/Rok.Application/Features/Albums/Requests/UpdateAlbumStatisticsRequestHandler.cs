using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class UpdateAlbumStatisticsRequest(long id) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;

    public int TrackCount { get; set; }

    public long Duration { get; set; }
}

public sealed class UpdateAlbumStatisticsRequestValidator : Validator<UpdateAlbumStatisticsRequest>
{
    public UpdateAlbumStatisticsRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}


public class UpdateAlbumStatisticsRequestHandler(IAlbumRepository _albumRepository) : IRequestHandler<UpdateAlbumStatisticsRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateAlbumStatisticsRequest message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.UpdateStatisticsAsync(message.Id, message.TrackCount, message.Duration);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("album.statistics_update_failed", "Failed to update album statistics."));
    }
}