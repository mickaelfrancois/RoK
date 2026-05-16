using Rok.Application.Features.Artists.Requests;

namespace Rok.ApplicationTests.Features.Artists.Validators;

public class UpdateArtistRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        UpdateArtistRequestValidator sut = new();
        UpdateArtistRequest request = new() { Id = 0 };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        UpdateArtistRequestValidator sut = new();
        UpdateArtistRequest request = new() { Id = 42 };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
