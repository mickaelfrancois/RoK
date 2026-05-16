using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;


public class UpdateArtistPictureDominantColorRequest(long id, long? colorValue) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;

    public long? ColorValue { get; init; } = colorValue;
}

public sealed class UpdateArtistPictureDominantColorRequestValidator : Validator<UpdateArtistPictureDominantColorRequest>
{
    public UpdateArtistPictureDominantColorRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class UpdateArtistPictureDominantColorRequestHandler(IArtistRepository _artistRepository) : IRequestHandler<UpdateArtistPictureDominantColorRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateArtistPictureDominantColorRequest message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.UpdatePictureDominantColorAsync(message.Id, message.ColorValue);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("artist.picture_dominant_color_update_failed", "Failed to update artist picture dominant color."));
    }
}
