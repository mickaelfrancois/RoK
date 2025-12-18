using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;

public class UpdateArtistGetMetaDataLastAttemptCommand(long id) : ICommand<Result<bool>>
{
    public long ArtistId { get; init; } = id;
}


internal class UpdateArtistGetMetaDataLastAttemptCommandHandler(IArtistRepository _repository) : ICommandHandler<UpdateArtistGetMetaDataLastAttemptCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateArtistGetMetaDataLastAttemptCommand request, CancellationToken cancellationToken)
    {
        bool result = await _repository.UpdateGetMetaDataLastAttemptAsync(request.ArtistId);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update meta last attempt.");
    }
}
