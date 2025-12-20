using Rok.Application.Interfaces;

namespace Rok.Application.Features.Genres.Command;

public class ResetGenreListenCountCommand : ICommand<Result<bool>>
{
}

public class ResetGenreListenCountCommandHandler(IGenreRepository _genreRepository) : ICommandHandler<ResetGenreListenCountCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(ResetGenreListenCountCommand message, CancellationToken cancellationToken)
    {
        bool result = await _genreRepository.ResetListenCountAsync();

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to reset genre listen count.");
    }
}
