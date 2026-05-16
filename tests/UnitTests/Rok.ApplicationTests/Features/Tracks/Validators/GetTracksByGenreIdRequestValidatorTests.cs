using Rok.Application.Features.Tracks.Requests;

namespace Rok.ApplicationTests.Features.Tracks.Validators;

public class GetTracksByGenreIdRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_genre_id_is_zero")]
    public async Task fails_when_genre_id_is_zero()
    {
        GetTracksByGenreIdRequestValidator sut = new();
        GetTracksByGenreIdRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_genre_id_is_positive")]
    public async Task succeeds_when_genre_id_is_positive()
    {
        GetTracksByGenreIdRequestValidator sut = new();
        GetTracksByGenreIdRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
