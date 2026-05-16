using Rok.Application.Features.Genres.Requests;

namespace Rok.ApplicationTests.Features.Genres.Validators;

public class UpdateGenreFavoriteRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        UpdateGenreFavoriteRequestValidator sut = new();
        UpdateGenreFavoriteRequest request = new(0, true);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        UpdateGenreFavoriteRequestValidator sut = new();
        UpdateGenreFavoriteRequest request = new(42, true);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
