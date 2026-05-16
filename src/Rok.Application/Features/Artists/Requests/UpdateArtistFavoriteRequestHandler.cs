using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class UpdateArtistFavoriteRequest(long id, bool isFavorite) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;

    public bool IsFavorite { get; init; } = isFavorite;
}

public sealed class UpdateArtistFavoriteRequestValidator : Validator<UpdateArtistFavoriteRequest>
{
    public UpdateArtistFavoriteRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class UpdateArtistFavoriteRequestHandler(IArtistRepository _artistRepository) : IRequestHandler<UpdateArtistFavoriteRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateArtistFavoriteRequest message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.UpdateFavoriteAsync(message.Id, message.IsFavorite);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("artist.favorite_update_failed", "Failed to update artist favorite status."));
    }
}