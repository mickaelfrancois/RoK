using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class UpdateArtistGetMetaDataLastAttemptRequest(long id) : IRequest<Result<bool>>
{
    public long ArtistId { get; init; } = id;
}


internal class UpdateArtistGetMetaDataLastAttemptRequestHandler(IArtistRepository _repository) : IRequestHandler<UpdateArtistGetMetaDataLastAttemptRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateArtistGetMetaDataLastAttemptRequest request, CancellationToken cancellationToken)
    {
        bool result = await _repository.UpdateGetMetaDataLastAttemptAsync(request.ArtistId);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("artist.meta_attempt_update_failed", "Failed to update meta last attempt."));
    }
}
