namespace Rok.Application.Features.Artists.Requests;

public sealed class GetArtistMergePreviewRequestValidator : Validator<GetArtistMergePreviewRequest>
{
    public GetArtistMergePreviewRequestValidator()
    {
        Rule(x => x.SourceId).GreaterThan(0L);
        Rule(x => x.TargetId).GreaterThan(0L);
    }
}