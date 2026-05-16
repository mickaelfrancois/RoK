using Rok.Application.Features.ListeningEvents.Requests;

namespace Rok.ApplicationTests.Features.ListeningEvents.Validators;

public class CreateListeningEventRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_track_id_is_zero")]
    public async Task fails_when_track_id_is_zero()
    {
        CreateListeningEventRequestValidator sut = new();
        CreateListeningEventRequest request = new() { TrackId = 0 };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_track_id_is_positive")]
    public async Task succeeds_when_track_id_is_positive()
    {
        CreateListeningEventRequestValidator sut = new();
        CreateListeningEventRequest request = new() { TrackId = 42 };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}