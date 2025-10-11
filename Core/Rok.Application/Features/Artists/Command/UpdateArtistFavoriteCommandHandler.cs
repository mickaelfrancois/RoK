using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;

public class UpdateArtistFavoriteCommand(long id, bool isFavorite) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;

    public bool IsFavorite { get; init; } = isFavorite;
}

public class UpdateArtistFavoriteCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<UpdateArtistFavoriteCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateArtistFavoriteCommand message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.UpdateFavoriteAsync(message.Id, message.IsFavorite);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update artist favorite status.");
    }
}
