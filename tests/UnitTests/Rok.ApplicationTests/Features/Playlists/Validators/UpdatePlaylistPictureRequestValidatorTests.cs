using Rok.Application.Features.Playlists.Requests;

namespace Rok.ApplicationTests.Features.Playlists.Validators;

public class UpdatePlaylistPictureRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        UpdatePlaylistPictureRequestValidator sut = new();
        UpdatePlaylistPictureRequest request = new() { Id = 0 };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        UpdatePlaylistPictureRequestValidator sut = new();
        UpdatePlaylistPictureRequest request = new() { Id = 42 };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
