using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Command;


public class UpdateAlbumPictureDominantColorCommand(long id, long? colorValue) : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; init; } = id;

    public long? ColorValue { get; init; } = colorValue;
}

public class UpdateAlbumPictureDominantColorCommandHandler(IAlbumRepository _albumRepository) : ICommandHandler<UpdateAlbumPictureDominantColorCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateAlbumPictureDominantColorCommand message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.UpdatePictureDominantColorAsync(message.Id, message.ColorValue);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update album picture dominant color.");
    }
}
