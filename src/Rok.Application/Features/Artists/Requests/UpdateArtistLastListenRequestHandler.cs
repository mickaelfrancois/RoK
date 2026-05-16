using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class UpdateArtistLastListenRequest(long id) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;
}

public sealed class UpdateArtistLastListenRequestValidator : Validator<UpdateArtistLastListenRequest>
{
    public UpdateArtistLastListenRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class UpdateArtistLastListenRequestHandler(IArtistRepository _artistRepository) : IRequestHandler<UpdateArtistLastListenRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateArtistLastListenRequest message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.UpdateLastListenAsync(message.Id);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update artist last listen status.");
    }
}
