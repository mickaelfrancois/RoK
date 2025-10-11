using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;

public class UpdateArtistLastListenCommand(long id) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;
}

public class UpdateArtistLastListenCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<UpdateArtistLastListenCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateArtistLastListenCommand message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.UpdateLastListenAsync(message.Id);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update artist last listen status.");
    }
}
