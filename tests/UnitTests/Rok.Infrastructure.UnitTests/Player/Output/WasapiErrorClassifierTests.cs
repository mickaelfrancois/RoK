using System.Runtime.InteropServices;
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

    [Theory(DisplayName = "exclusive_failure_maps_com_errors_to_fallback_reasons")]
    [InlineData(unchecked((int)0x88890008), EExclusiveFallbackReason.FormatRefused)]
    [InlineData(unchecked((int)0x8889000A), EExclusiveFallbackReason.DeviceBusy)]
    [InlineData(unchecked((int)0x8889000E), EExclusiveFallbackReason.ExclusiveDisabled)]
    [InlineData(unchecked((int)0x80004005), EExclusiveFallbackReason.Other)]
    public void ClassifyExclusiveFailure_MapsComErrorToReason(int hresult, EExclusiveFallbackReason expected)
    {
        // Act
        EExclusiveFallbackReason? reason = WasapiErrorClassifier.ClassifyExclusiveFailure(new COMException("refused", hresult));

        // Assert
        Assert.Equal(expected, reason);
    }

    [Fact(DisplayName = "exclusive_failure_lets_an_invalidated_device_propagate")]
    public void ClassifyExclusiveFailure_ReturnsNull_ForInvalidatedDevice()
    {
        // Act
        EExclusiveFallbackReason? reason = WasapiErrorClassifier.ClassifyExclusiveFailure(new COMException("gone", unchecked((int)0x88890004)));

        // Assert
        Assert.Null(reason);
    }

    [Fact(DisplayName = "exclusive_failure_treats_a_refused_sample_rate_as_format_refused")]
    public void ClassifyExclusiveFailure_ReturnsFormatRefused_ForNotSupported()
    {
        // Act
        EExclusiveFallbackReason? reason = WasapiErrorClassifier.ClassifyExclusiveFailure(new NotSupportedException("sample rate"));

        // Assert
        Assert.Equal(EExclusiveFallbackReason.FormatRefused, reason);
    }

    [Theory(DisplayName = "exclusive_failure_lets_unexpected_exceptions_propagate")]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    public void ClassifyExclusiveFailure_ReturnsNull_ForUnexpectedException(Type exceptionType)
    {
        // Arrange
        Exception exception = (Exception)Activator.CreateInstance(exceptionType)!;

        // Act
        EExclusiveFallbackReason? reason = WasapiErrorClassifier.ClassifyExclusiveFailure(exception);

        // Assert
        Assert.Null(reason);
    }

    [Fact(DisplayName = "classifier_treats_an_invalidated_device_as_a_lost_output")]
    public void IsDeviceInvalidated_RecognisesInvalidatedDevice()
    {
        // Act & Assert
        Assert.True(WasapiErrorClassifier.IsDeviceInvalidated(unchecked((int)0x88890004)));
        Assert.False(WasapiErrorClassifier.IsDeviceInvalidated(unchecked((int)0x8889000A)));
    }
}