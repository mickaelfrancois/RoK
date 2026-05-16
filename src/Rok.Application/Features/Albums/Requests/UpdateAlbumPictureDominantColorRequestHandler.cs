using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class UpdateAlbumPictureDominantColorRequest(long id, long? colorValue) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;

    public long? ColorValue { get; init; } = colorValue;
}

public sealed class UpdateAlbumPictureDominantColorRequestValidator : Validator<UpdateAlbumPictureDominantColorRequest>
{
    public UpdateAlbumPictureDominantColorRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}

public class UpdateAlbumPictureDominantColorRequestHandler(IAlbumRepository _albumRepository) : IRequestHandler<UpdateAlbumPictureDominantColorRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateAlbumPictureDominantColorRequest message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.UpdatePictureDominantColorAsync(message.Id, message.ColorValue);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("album.picture_dominant_color_update_failed", "Failed to update album picture dominant color."));
    }
}
