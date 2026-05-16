using Rok.Application.Features.Albums.Requests;

namespace Rok.ApplicationTests.Features.Albums.Validators;

public class GetAlbumsByGenreIdRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_genre_id_is_zero")]
    public async Task fails_when_genre_id_is_zero()
    {
        GetAlbumsByGenreIdRequestValidator sut = new();
        GetAlbumsByGenreIdRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_genre_id_is_positive")]
    public async Task succeeds_when_genre_id_is_positive()
    {
        GetAlbumsByGenreIdRequestValidator sut = new();
        GetAlbumsByGenreIdRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
