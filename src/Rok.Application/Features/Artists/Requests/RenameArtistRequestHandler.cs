using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Artists.Requests;

public class RenameArtistRequestHandler(ArtistRenameService artistRenameService) : IRequestHandler<RenameArtistRequest, Result<ArtistRenameOutcome>>
{
    public Task<Result<ArtistRenameOutcome>> Handle(RenameArtistRequest request, CancellationToken cancellationToken)
    {
        return artistRenameService.RenameAsync(request.ArtistId, request.NewName, request.Progress, cancellationToken);
    }
}