using Rok.Application.Features.Albums.Requests;

namespace Rok.ApplicationTests.Features.Albums.Validators;

public class UpdateAlbumPictureDominantColorRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        UpdateAlbumPictureDominantColorRequestValidator sut = new();
        UpdateAlbumPictureDominantColorRequest request = new(0, null);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        UpdateAlbumPictureDominantColorRequestValidator sut = new();
        UpdateAlbumPictureDominantColorRequest request = new(42, null);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
