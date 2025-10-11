using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Command;

public class UpdateAlbumGetMetaDataLastAttemptCommand(long id) : ICommand<Result<bool>>
{
    public long AlbumId { get; init; } = id;
}


internal class UpdateAlbumGetMetaDataLastAttemptCommandHandler(IArtistRepository _repository) : ICommandHandler<UpdateAlbumGetMetaDataLastAttemptCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateAlbumGetMetaDataLastAttemptCommand request, CancellationToken cancellationToken)
    {
        bool result = await _repository.UpdateGetMetaDataLastAttemptAsync(request.AlbumId);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update meta last attempt.");
    }
}
