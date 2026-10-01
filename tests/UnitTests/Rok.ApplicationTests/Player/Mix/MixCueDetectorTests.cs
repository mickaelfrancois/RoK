using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player.Mix;

public class MixCueDetectorTests
{
    [Fact(DisplayName = "detects_trailing_silence")]
    public void DetectOutro_TrailingSilence_PutsMusicEndAtTheSilenceStart()
    {
        // Arrange
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Sine(25), SyntheticSignal.Silence(5)));

        // Act
        var cues = MixCueDetector.DetectOutro(envelope);

        // Assert
        Assert.NotNull(cues);
        Assert.InRange(cues.MusicEndSeconds, 24.95, 25.05);
        Assert.Equal(0, cues.FadeOutSeconds);
    }

    [Fact(DisplayName = "detects_a_linear_fade_out")]
    public void DetectOutro_LinearFade_FindsTheNaturalFade()
    {
        // Arrange
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Sine(20), SyntheticSignal.Ramp(8, 1, 0)));

        // Act
        var cues = MixCueDetector.DetectOutro(envelope);

        // Assert
        Assert.NotNull(cues);
        Assert.InRange(cues.MusicEndSeconds, 27.5, 28.0);
        Assert.InRange(cues.FadeOutSeconds, 5, 8.5);
    }

    [Fact(DisplayName = "detects_leading_silence")]
    public void DetectIntro_LeadingSilence_StartsJustBeforeTheMusic()
    {
        // Arrange
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Silence(3), SyntheticSignal.Sine(10)));

        // Act
        var cues = MixCueDetector.DetectIntro(envelope);

        // Assert
        Assert.NotNull(cues);
        Assert.InRange(cues.MusicStartSeconds, 2.85, 2.95);
    }

    [Fact(DisplayName = "no_silence_puts_cues_at_the_bounds")]
    public void Detect_ContinuousSignal_PutsCuesAtTheBounds()
    {
        // Arrange
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Sine(30));

        // Act
        var outro = MixCueDetector.DetectOutro(envelope);
        var intro = MixCueDetector.DetectIntro(envelope);

        // Assert
        Assert.NotNull(outro);
        Assert.NotNull(intro);
        Assert.Equal(30, outro.MusicEndSeconds);
        Assert.Equal(0, outro.FadeOutSeconds);
        Assert.Equal(0, intro.MusicStartSeconds);
    }

    [Fact(DisplayName = "noise_floor_below_threshold_is_silence")]
    public void DetectOutro_NoiseFloorAtTheEnd_IsTreatedAsSilence()
    {
        // Arrange
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Sine(20), SyntheticSignal.Noise(5, -60)));

        // Act
        var cues = MixCueDetector.DetectOutro(envelope);

        // Assert
        Assert.NotNull(cues);
        Assert.InRange(cues.MusicEndSeconds, 19.95, 20.05);
    }

    [Fact(DisplayName = "quiet_track_pianissimo_is_not_silence")]
    public void DetectOutro_QuietTrackWithPianissimoEnd_KeepsTheEnding()
    {
        // Arrange
        var bodyAmplitude = Math.Pow(10, -35 / 20.0) * Math.Sqrt(2);
        var passageAmplitude = Math.Pow(10, -55 / 20.0) * Math.Sqrt(2);
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Concat(
            SyntheticSignal.Sine(27, bodyAmplitude),
            SyntheticSignal.Sine(3, passageAmplitude)));

        // Act
        var cues = MixCueDetector.DetectOutro(envelope);

        // Assert
        Assert.NotNull(cues);
        Assert.Equal(30, cues.MusicEndSeconds);
    }

    [Fact(DisplayName = "fully_silent_window_yields_no_cue")]
    public void Detect_OnlyZeros_ReturnsNull()
    {
        // Arrange
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Silence(10));

        // Act and Assert
        Assert.Null(MixCueDetector.DetectOutro(envelope));
        Assert.Null(MixCueDetector.DetectIntro(envelope));
    }

    [Fact(DisplayName = "empty_envelope_yields_no_cue")]
    public void Detect_EmptyEnvelope_ReturnsNull()
    {
        // Arrange
        var envelope = new RmsEnvelope(0, 0.05, [], 10);

        // Act and Assert
        Assert.Null(MixCueDetector.DetectOutro(envelope));
        Assert.Null(MixCueDetector.DetectIntro(envelope));
    }

    [Fact(DisplayName = "tail_envelope_cues_are_absolute_positions")]
    public void DetectOutro_TailEnvelope_ReturnsAbsolutePositions()
    {
        // Arrange
        var envelope = SyntheticSignal.Envelope(
            SyntheticSignal.Concat(SyntheticSignal.Sine(20), SyntheticSignal.Silence(10)),
            startSeconds: 150,
            trackLengthSeconds: 180);

        // Act
        var cues = MixCueDetector.DetectOutro(envelope);

        // Assert
        Assert.NotNull(cues);
        Assert.InRange(cues.MusicEndSeconds, 169.95, 170.05);
    }

    [Fact(DisplayName = "short_slope_is_not_a_natural_fade")]
    public void DetectOutro_OneSecondSlope_IsAnAbruptEnding()
    {
        // Arrange
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Sine(20), SyntheticSignal.Ramp(1, 1, 0)));

        // Act
        var cues = MixCueDetector.DetectOutro(envelope);

        // Assert
        Assert.NotNull(cues);
        Assert.Equal(0, cues.FadeOutSeconds);
    }
}