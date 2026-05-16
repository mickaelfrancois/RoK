using Rok.Application.Features.Playlists.Requests;

namespace Rok.ApplicationTests.Features.Playlists.Validators;

public class GetPlaylistByIdRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        GetPlaylistByIdRequestValidator sut = new();
        GetPlaylistByIdRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        GetPlaylistByIdRequestValidator sut = new();
        GetPlaylistByIdRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}