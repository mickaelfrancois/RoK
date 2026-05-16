using Rok.Application.Features.Artists.Requests;

namespace Rok.ApplicationTests.Features.Artists.Validators;

public class GetArtistByNameRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_name_is_empty")]
    public async Task fails_when_name_is_empty()
    {
        GetArtistByNameRequestValidator sut = new();
        GetArtistByNameRequest request = new(string.Empty);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_name_is_provided")]
    public async Task succeeds_when_name_is_provided()
    {
        GetArtistByNameRequestValidator sut = new();
        GetArtistByNameRequest request = new("Pink Floyd");

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
