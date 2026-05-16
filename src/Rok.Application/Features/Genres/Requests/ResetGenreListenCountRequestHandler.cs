using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Genres.Requests;

public class ResetGenreListenCountRequest : IRequest<Result<bool>>
{
}

public class ResetGenreListenCountRequestHandler(IGenreRepository _genreRepository) : IRequestHandler<ResetGenreListenCountRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResetGenreListenCountRequest message, CancellationToken cancellationToken)
    {
        bool result = await _genreRepository.ResetListenCountAsync();

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("genre.listen_count_reset_failed", "Failed to reset genre listen count."));
    }
}
