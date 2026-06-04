using Rok.Application.Features.Albums.Requests;

namespace Rok.ApplicationTests.Features.Albums.Validators;

public class GetAlbumListeningStatsRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_album_id_is_zero")]
    public async Task fails_when_album_id_is_zero()
    {
        GetAlbumListeningStatsRequestValidator sut = new();
        GetAlbumListeningStatsRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_album_id_is_positive")]
    public async Task succeeds_when_album_id_is_positive()
    {
        GetAlbumListeningStatsRequestValidator sut = new();
        GetAlbumListeningStatsRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
