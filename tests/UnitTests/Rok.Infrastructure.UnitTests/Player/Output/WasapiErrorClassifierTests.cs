using Rok.Application.Player.Output;
using Rok.Infrastructure.Player.Output;

namespace Rok.Infrastructure.UnitTests.Player.Output;

public class WasapiErrorClassifierTests
{
    [Theory(DisplayName = "classifier_maps_hresults_to_fallback_reasons")]
    [InlineData(unchecked((int)0x88890008), EExclusiveFallbackReason.FormatRefused)]
    [InlineData(unchecked((int)0x8889000A), EExclusiveFallbackReason.DeviceBusy)]
    [InlineData(unchecked((int)0x8889000E), EExclusiveFallbackReason.ExclusiveDisabled)]
    [InlineData(unchecked((int)0x80004005), EExclusiveFallbackReason.Other)]
    public void Classify_MapsHResultToReason(int hresult, EExclusiveFallbackReason expected)
    {
        // Act
        EExclusiveFallbackReason reason = WasapiErrorClassifier.Classify(hresult);

        // Assert
        Assert.Equal(expected, reason);
    }

    [Fact(DisplayName = "classifier_treats_an_invalidated_device_as_a_lost_output")]
    public void IsDeviceInvalidated_RecognisesInvalidatedDevice()
    {
        // Act & Assert
        Assert.True(WasapiErrorClassifier.IsDeviceInvalidated(unchecked((int)0x88890004)));
        Assert.False(WasapiErrorClassifier.IsDeviceInvalidated(unchecked((int)0x8889000A)));
    }
}