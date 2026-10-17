using Rok.Application.Features.Artists.Merge;

namespace Rok.Application.Features.Artists.Requests;

public class MergeArtistsRequestHandler(ArtistMergeService artistMergeService) : IRequestHandler<MergeArtistsRequest, Result<ArtistMergeOutcome>>
{
    public Task<Result<ArtistMergeOutcome>> Handle(MergeArtistsRequest request, CancellationToken cancellationToken)
    {
        return artistMergeService.MergeAsync(request.SourceId, request.TargetId, request.Progress, cancellationToken);
    }
}