namespace Rok.Application.Features.Tracks.Requests;

public sealed class UpdateTrackTagsRequestValidator : Validator<UpdateTrackTagsRequest>
{
    public UpdateTrackTagsRequestValidator()
    {
        Rule(x => x.TrackId).GreaterThan(0L);
        Rule(x => x.Title).Must(title => title is null || !string.IsNullOrWhiteSpace(title)).Message("Title must not be blank.");
        Rule(x => x.TrackNumber).Must(n => n is null or >= 1 and <= TrackEditLimits.MaxTrackNumber).Message($"Track number must be between 1 and {TrackEditLimits.MaxTrackNumber}.");
        Rule(x => x.Disc).Must(n => n is null or >= 1 and <= TrackEditLimits.MaxDisc).Message($"Disc must be between 1 and {TrackEditLimits.MaxDisc}.");
        Rule(x => x.Bpm).Must(n => n is null or >= 0 and <= TrackEditLimits.MaxBpm).Message($"BPM must be between 0 and {TrackEditLimits.MaxBpm}.");
        Rule(x => x.HasChanges).Must(hasChanges => hasChanges).Message("At least one field must be edited.");
    }
}