using Rok.Application.Features.EqualizerPresets.Requests;

namespace Rok.ApplicationTests.Features.EqualizerPresets.Validators;

public class SaveEqualizerPresetRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_bands_is_null")]
    public async Task fails_when_bands_is_null()
    {
        SaveEqualizerPresetRequestValidator sut = new();
        SaveEqualizerPresetRequest request = new() { Bands = null! };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_bands_is_provided")]
    public async Task succeeds_when_bands_is_provided()
    {
        SaveEqualizerPresetRequestValidator sut = new();
        SaveEqualizerPresetRequest request = new() { Bands = new float[10] };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
