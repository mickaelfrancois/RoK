using Rok.Application.Features.Albums.Requests;

namespace Rok.ApplicationTests.Features.Albums.Validators;

public class UpdateAlbumTagsRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        UpdateAlbumTagsRequestValidator sut = new();
        UpdateAlbumTagsRequest request = new(0, Array.Empty<string>());

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        UpdateAlbumTagsRequestValidator sut = new();
        UpdateAlbumTagsRequest request = new(42, Array.Empty<string>());

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
