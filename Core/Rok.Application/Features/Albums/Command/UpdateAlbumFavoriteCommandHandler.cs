using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Command;

public class UpdateAlbumFavoriteCommand(long id, bool isFavorite) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;

    public bool IsFavorite { get; init; } = isFavorite;
}

public class UpdateAlbumFavoriteCommandHandler(IAlbumRepository _albumRepository) : ICommandHandler<UpdateAlbumFavoriteCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateAlbumFavoriteCommand message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.UpdateFavoriteAsync(message.Id, message.IsFavorite);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update album favorite status.");
    }
}
