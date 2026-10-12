namespace Rok.Application.Features.Tracks.Requests;

public sealed class UpdateTrackTitleRequestValidator : Validator<UpdateTrackTitleRequest>
{
    public UpdateTrackTitleRequestValidator()
    {
        Rule(x => x.TrackId).GreaterThan(0L);
        Rule(x => x.Title).Must(title => !string.IsNullOrWhiteSpace(title)).Message("Title must not be empty.");
    }
}