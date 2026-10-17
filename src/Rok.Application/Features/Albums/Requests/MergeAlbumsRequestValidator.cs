namespace Rok.Application.Features.Albums.Requests;

public sealed class MergeAlbumsRequestValidator : Validator<MergeAlbumsRequest>
{
    public MergeAlbumsRequestValidator()
    {
        Rule(x => x.SourceId).GreaterThan(0L);
        Rule(x => x.TargetId).GreaterThan(0L);
    }
}