using Rok.Application.Player.Output;

namespace Rok.ApplicationTests.Player.Output;

public class AudioOutputStatusResolverTests
{
    private static AudioOutputState Exclusive(bool isNeutral) => new(true, "dac", true, EAudioOutputMode.Exclusive, null, isNeutral);

    [Fact(DisplayName = "status_is_idle_when_no_output_is_open")]
    public void Resolve_ReturnsIdle_WhenNoOutputIsOpen()
    {
        // Act
        EAudioOutputStatus status = AudioOutputStatusResolver.Resolve(AudioOutputState.Closed, isLive: false);

        // Assert
        Assert.Equal(EAudioOutputStatus.Idle, status);
    }

    [Fact(DisplayName = "status_is_exclusive_bit_perfect_when_processing_is_neutral")]
    public void Resolve_ReturnsBitPerfect_WhenProcessingIsNeutral()
    {
        // Act
        EAudioOutputStatus status = AudioOutputStatusResolver.Resolve(Exclusive(isNeutral: true), isLive: false);

        // Assert
        Assert.Equal(EAudioOutputStatus.ExclusiveBitPerfect, status);
    }

    [Fact(DisplayName = "status_is_exclusive_processed_when_eq_gain_or_volume_active")]
    public void Resolve_ReturnsProcessed_WhenProcessingIsActive()
    {
        // Act
        EAudioOutputStatus status = AudioOutputStatusResolver.Resolve(Exclusive(isNeutral: false), isLive: false);

        // Assert
        Assert.Equal(EAudioOutputStatus.ExclusiveProcessed, status);
    }

    [Theory(DisplayName = "status_is_shared_fallback_when_exclusive_failed")]
    [InlineData(EExclusiveFallbackReason.FormatRefused)]
    [InlineData(EExclusiveFallbackReason.DeviceBusy)]
    [InlineData(EExclusiveFallbackReason.ExclusiveDisabled)]
    [InlineData(EExclusiveFallbackReason.Other)]
    public void Resolve_ReturnsSharedFallback_WhenExclusiveFailed(EExclusiveFallbackReason reason)
    {
        // Arrange
        AudioOutputState state = new(true, "dac", true, EAudioOutputMode.Shared, reason, true);

        // Act
        EAudioOutputStatus status = AudioOutputStatusResolver.Resolve(state, isLive: false);

        // Assert
        Assert.Equal(EAudioOutputStatus.SharedFallback, status);
    }

    [Fact(DisplayName = "status_is_shared_for_a_plain_shared_output")]
    public void Resolve_ReturnsShared_ForSharedOutput()
    {
        // Arrange
        AudioOutputState state = new(true, "speakers", true, EAudioOutputMode.Shared, null, false);

        // Act
        EAudioOutputStatus status = AudioOutputStatusResolver.Resolve(state, isLive: false);

        // Assert
        Assert.Equal(EAudioOutputStatus.Shared, status);
    }

    [Fact(DisplayName = "status_is_shared_for_radio_even_in_exclusive_mode")]
    public void Resolve_ReturnsShared_ForRadio()
    {
        // Act
        EAudioOutputStatus status = AudioOutputStatusResolver.Resolve(Exclusive(isNeutral: true), isLive: true);

        // Assert
        Assert.Equal(EAudioOutputStatus.Shared, status);
    }
}