using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;

public class ResetArtistListenCountCommand : ICommand<Result<bool>>
{
}

public class ResetArtistListenCountCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<ResetArtistListenCountCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(ResetArtistListenCountCommand message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.ResetListenCountAsync();

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to reset artist listen count.");
    }
}
