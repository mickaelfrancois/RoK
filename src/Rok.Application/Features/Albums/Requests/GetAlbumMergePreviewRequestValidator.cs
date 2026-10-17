namespace Rok.Application.Features.Albums.Requests;

public sealed class GetAlbumMergePreviewRequestValidator : Validator<GetAlbumMergePreviewRequest>
{
    public GetAlbumMergePreviewRequestValidator()
    {
        Rule(x => x.SourceId).GreaterThan(0L);
        Rule(x => x.TargetId).GreaterThan(0L);
    }
}