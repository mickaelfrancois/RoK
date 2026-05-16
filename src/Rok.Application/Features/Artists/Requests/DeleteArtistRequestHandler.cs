using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class DeleteArtistRequest : IRequest<Result<bool>>
{
    public long Id { get; set; }
}

public sealed class DeleteArtistRequestValidator : Validator<DeleteArtistRequest>
{
    public DeleteArtistRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class DeleteArtistRequestHandler(IArtistRepository _artistRepository) : IRequestHandler<DeleteArtistRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteArtistRequest message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.DeleteAsync(new ArtistEntity { Id = message.Id });

        if (result)
            return Result<bool>.Ok(true);
        else
            return Result<bool>.Fail(new OperationError("artist.delete_failed", "Failed to delete artist."));
    }
}