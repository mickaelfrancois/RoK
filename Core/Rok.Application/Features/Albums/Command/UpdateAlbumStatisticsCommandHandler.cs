using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Command;

public class UpdateAlbumStatisticsCommand(long id) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;

    public int TrackCount { get; set; }

    public long Duration { get; set; }
}


public class UpdateAlbumStatisticsCommandHandler(IAlbumRepository _albumRepository) : ICommandHandler<UpdateAlbumStatisticsCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateAlbumStatisticsCommand message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.UpdateStatisticsAsync(message.Id, message.TrackCount, message.Duration);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update album statistics.");
    }
}