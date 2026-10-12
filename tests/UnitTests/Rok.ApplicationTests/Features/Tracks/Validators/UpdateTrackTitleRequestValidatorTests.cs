using Rok.Application.Features.Tracks.Requests;

namespace Rok.ApplicationTests.Features.Tracks.Validators;

public class UpdateTrackTitleRequestValidatorTests
{
    [Theory(DisplayName = "fails_when_title_is_empty_or_whitespace")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task fails_when_title_is_empty_or_whitespace(string title)
    {
        UpdateTrackTitleRequestValidator sut = new();

        ValidationResult result = await sut.ValidateAsync(new UpdateTrackTitleRequest(1, title), CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Theory(DisplayName = "fails_when_track_id_is_not_positive")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task fails_when_track_id_is_not_positive(long trackId)
    {
        UpdateTrackTitleRequestValidator sut = new();

        ValidationResult result = await sut.ValidateAsync(new UpdateTrackTitleRequest(trackId, "Title"), CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive_and_title_is_not_blank")]
    public async Task succeeds_when_id_is_positive_and_title_is_not_blank()
    {
        UpdateTrackTitleRequestValidator sut = new();

        ValidationResult result = await sut.ValidateAsync(new UpdateTrackTitleRequest(1, "Title"), CancellationToken.None);

        Assert.True(result.IsValid);
    }
}