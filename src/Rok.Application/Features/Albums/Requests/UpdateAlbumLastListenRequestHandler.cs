using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class UpdateAlbumLastListenRequest(long id) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;
}

public sealed class UpdateAlbumLastListenRequestValidator : Validator<UpdateAlbumLastListenRequest>
{
    public UpdateAlbumLastListenRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}

public class UpdateAlbumLastListenRequestHandler(IAlbumRepository _albumRepository) : IRequestHandler<UpdateAlbumLastListenRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateAlbumLastListenRequest message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.UpdateLastListenAsync(message.Id);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update album last listen status.");
    }
}
