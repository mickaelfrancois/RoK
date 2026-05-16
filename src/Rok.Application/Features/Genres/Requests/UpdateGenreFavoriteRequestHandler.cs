using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Genres.Requests;

public class UpdateGenreFavoriteRequest(long id, bool isFavorite) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;

    public bool IsFavorite { get; init; } = isFavorite;
}

public sealed class UpdateGenreFavoriteRequestValidator : Validator<UpdateGenreFavoriteRequest>
{
    public UpdateGenreFavoriteRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class UpdateGenreFavoriteRequestHandler(IGenreRepository _genreRepository) : IRequestHandler<UpdateGenreFavoriteRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateGenreFavoriteRequest message, CancellationToken cancellationToken)
    {
        bool result = await _genreRepository.UpdateFavoriteAsync(message.Id, message.IsFavorite);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("genre.favorite_update_failed", "Failed to update genre favorite status."));
    }
}
