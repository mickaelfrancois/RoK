using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;


public class UpdateArtistPictureDominantColorCommand(long id, long? colorValue) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;

    public long? ColorValue { get; init; } = colorValue;
}

public class UpdateArtistPictureDominantColorCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<UpdateArtistPictureDominantColorCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateArtistPictureDominantColorCommand message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.UpdatePictureDominantColorAsync(message.Id, message.ColorValue);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update artist picture dominant color.");
    }
}
