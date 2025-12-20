using Rok.Application.Interfaces;

namespace Rok.Application.Features.Genres.Command;

public class UpdateGenretLastListenCommand(long id) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;
}

public class UpdateGenretLastListenCommandHandler(IGenreRepository _genreRepository) : ICommandHandler<UpdateGenretLastListenCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateGenretLastListenCommand message, CancellationToken cancellationToken)
    {
        bool result = await _genreRepository.UpdateLastListenAsync(message.Id);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update genre last listen status.");
    }
}
