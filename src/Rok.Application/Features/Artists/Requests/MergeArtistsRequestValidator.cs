namespace Rok.Application.Features.Artists.Requests;

public sealed class MergeArtistsRequestValidator : Validator<MergeArtistsRequest>
{
    public MergeArtistsRequestValidator()
    {
        Rule(x => x.SourceId).GreaterThan(0L);
        Rule(x => x.TargetId).GreaterThan(0L);
    }
}