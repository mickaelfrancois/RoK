using Rok.Application.Interfaces;

namespace Rok.Application.Features.Genres.Command;

public class UpdateGenreFavoriteCommand(long id, bool isFavorite) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;

    public bool IsFavorite { get; init; } = isFavorite;
}

public class UpdateGenreFavoriteCommandHandler(IGenreRepository _genreRepository) : ICommandHandler<UpdateGenreFavoriteCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateGenreFavoriteCommand message, CancellationToken cancellationToken)
    {
        bool result = await _genreRepository.UpdateFavoriteAsync(message.Id, message.IsFavorite);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update genre favorite status.");
    }
}
