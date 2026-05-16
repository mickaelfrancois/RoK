using Rok.Application.Features.Playlists.Requests;

namespace Rok.ApplicationTests.Features.Playlists.Validators;

public class UpdatePlaylistRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        UpdatePlaylistRequestValidator sut = new();
        UpdatePlaylistRequest request = new() { Id = 0 };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        UpdatePlaylistRequestValidator sut = new();
        UpdatePlaylistRequest request = new() { Id = 42, Name = "Foo" };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
