using Rok.Application.Features.Albums.Requests;

namespace Rok.ApplicationTests.Features.Albums.Validators;

public class GetAlbumByIdRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        GetAlbumByIdRequestValidator sut = new();
        GetAlbumByIdRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        GetAlbumByIdRequestValidator sut = new();
        GetAlbumByIdRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
