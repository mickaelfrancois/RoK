using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Command;

public class ResetAlbumListenCountCommand : ICommand<Result<bool>>
{
}

public class ResetAlbumListenCountCommandHandler(IAlbumRepository _albumRepository) : ICommandHandler<ResetAlbumListenCountCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(ResetAlbumListenCountCommand message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.ResetListenCountAsync();

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to reset album listen count.");
    }
}
