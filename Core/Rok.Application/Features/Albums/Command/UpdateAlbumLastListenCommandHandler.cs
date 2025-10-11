using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Command;

public class UpdateAlbumLastListenCommand(long id) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;
}

public class UpdateAlbumLastListenCommandHandler(IAlbumRepository _albumRepository) : ICommandHandler<UpdateAlbumLastListenCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateAlbumLastListenCommand message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.UpdateLastListenAsync(message.Id);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update album last listen status.");
    }
}
