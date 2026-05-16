using Rok.Application.Features.Artists.Requests;

namespace Rok.ApplicationTests.Features.Artists.Validators;

public class UpdateArtistLastListenRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        UpdateArtistLastListenRequestValidator sut = new();
        UpdateArtistLastListenRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        UpdateArtistLastListenRequestValidator sut = new();
        UpdateArtistLastListenRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
