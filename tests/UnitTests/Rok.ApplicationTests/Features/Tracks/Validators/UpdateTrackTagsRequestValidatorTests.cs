using Rok.Application.Features.Tracks;
using Rok.Application.Features.Tracks.Requests;

namespace Rok.ApplicationTests.Features.Tracks.Validators;

public class UpdateTrackTagsRequestValidatorTests
{
    private static Task<ValidationResult> ValidateAsync(UpdateTrackTagsRequest request)
    {
        UpdateTrackTagsRequestValidator sut = new();

        return sut.ValidateAsync(request, CancellationToken.None);
    }

    [Theory(DisplayName = "fails_when_track_id_is_not_positive")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task fails_when_track_id_is_not_positive(long trackId)
    {
        ValidationResult result = await ValidateAsync(new UpdateTrackTagsRequest(trackId, "Title"));

        Assert.False(result.IsValid);
    }

    [Theory(DisplayName = "fails_when_title_is_blank")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task fails_when_title_is_blank(string title)
    {
        ValidationResult result = await ValidateAsync(new UpdateTrackTagsRequest(1, title));

        Assert.False(result.IsValid);
    }

    [Theory(DisplayName = "fails_when_track_number_is_out_of_bounds")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(TrackEditLimits.MaxTrackNumber + 1)]
    public async Task fails_when_track_number_is_out_of_bounds(int trackNumber)
    {
        ValidationResult result = await ValidateAsync(new UpdateTrackTagsRequest(1, trackNumber: trackNumber));

        Assert.False(result.IsValid);
    }

    [Theory(DisplayName = "fails_when_disc_is_out_of_bounds")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(TrackEditLimits.MaxDisc + 1)]
    public async Task fails_when_disc_is_out_of_bounds(int disc)
    {
        ValidationResult result = await ValidateAsync(new UpdateTrackTagsRequest(1, disc: disc));

        Assert.False(result.IsValid);
    }

    [Theory(DisplayName = "fails_when_bpm_is_out_of_bounds")]
    [InlineData(-1)]
    [InlineData(TrackEditLimits.MaxBpm + 1)]
    public async Task fails_when_bpm_is_out_of_bounds(int bpm)
    {
        ValidationResult result = await ValidateAsync(new UpdateTrackTagsRequest(1, bpm: bpm));

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "fails_when_no_field_is_edited")]
    public async Task fails_when_no_field_is_edited()
    {
        ValidationResult result = await ValidateAsync(new UpdateTrackTagsRequest(1));

        Assert.False(result.IsValid);
    }

    [Theory(DisplayName = "succeeds_on_the_bounds_of_each_field")]
    [InlineData(1, 1, 0, "A")]
    [InlineData(TrackEditLimits.MaxTrackNumber, TrackEditLimits.MaxDisc, TrackEditLimits.MaxBpm, "")]
    public async Task succeeds_on_the_bounds_of_each_field(int trackNumber, int disc, int bpm, string composers)
    {
        ValidationResult result = await ValidateAsync(new UpdateTrackTagsRequest(1, "Title", trackNumber, disc, composers, bpm));

        Assert.True(result.IsValid);
    }

    [Theory(DisplayName = "succeeds_when_a_single_field_is_edited")]
    [InlineData("Title", null, null, null, null)]
    [InlineData(null, 3, null, null, null)]
    [InlineData(null, null, 2, null, null)]
    [InlineData(null, null, null, "", null)]
    [InlineData(null, null, null, null, 0)]
    public async Task succeeds_when_a_single_field_is_edited(string? title, int? trackNumber, int? disc, string? composers, int? bpm)
    {
        ValidationResult result = await ValidateAsync(new UpdateTrackTagsRequest(1, title, trackNumber, disc, composers, bpm));

        Assert.True(result.IsValid);
    }
}