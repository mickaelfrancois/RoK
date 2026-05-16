using Rok.Application.Features.Genres.Requests;

namespace Rok.ApplicationTests.Features.Genres.Validators;

public class UpdateGenretLastListenRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        UpdateGenretLastListenRequestValidator sut = new();
        UpdateGenretLastListenRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        UpdateGenretLastListenRequestValidator sut = new();
        UpdateGenretLastListenRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
