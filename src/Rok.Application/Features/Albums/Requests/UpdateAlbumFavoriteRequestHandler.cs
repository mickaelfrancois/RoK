using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class UpdateAlbumFavoriteRequest(long id, bool isFavorite) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;

    public bool IsFavorite { get; init; } = isFavorite;
}

public sealed class UpdateAlbumFavoriteRequestValidator : Validator<UpdateAlbumFavoriteRequest>
{
    public UpdateAlbumFavoriteRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}

public class UpdateAlbumFavoriteRequestHandler(IAlbumRepository _albumRepository) : IRequestHandler<UpdateAlbumFavoriteRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateAlbumFavoriteRequest message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.UpdateFavoriteAsync(message.Id, message.IsFavorite);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("album.favorite_update_failed", "Failed to update album favorite status."));
    }
}
