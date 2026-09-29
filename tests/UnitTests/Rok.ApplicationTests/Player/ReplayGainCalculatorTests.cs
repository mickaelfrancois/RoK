using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class ReplayGainCalculatorTests
{
    private const float Tolerance = 1e-4f;

    private static TrackDto BuildTrack(double? trackGain = null, double? trackPeak = null, double? albumGain = null, double? albumPeak = null, long? albumId = null, int? trackNumber = null) =>
        new()
        {
            Id = trackNumber ?? 0,
            AlbumId = albumId,
            TrackNumber = trackNumber,
            ReplayGainTrackGain = trackGain,
            ReplayGainTrackPeak = trackPeak,
            ReplayGainAlbumGain = albumGain,
            ReplayGainAlbumPeak = albumPeak
        };

    private static float Linear(double db) => (float)Math.Pow(10, db / 20);

    [Fact(DisplayName = "resolve_returns_one_when_mode_is_off")]
    public void Resolve_ReturnsOne_WhenModeIsOff()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Off, 6, null, BuildTrack(trackGain: -8), null);

        // Assert
        Assert.Equal(1f, gain);
    }

    [Theory(DisplayName = "resolve_returns_one_without_any_tag_even_with_a_preamp")]
    [InlineData(EReplayGainMode.Track)]
    [InlineData(EReplayGainMode.Album)]
    [InlineData(EReplayGainMode.Auto)]
    public void Resolve_ReturnsOne_WithoutAnyTag_EvenWithPreamp(EReplayGainMode mode)
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(mode, 6, null, BuildTrack(), null);

        // Assert
        Assert.Equal(1f, gain);
    }

    [Fact(DisplayName = "resolve_applies_the_track_gain_in_track_mode")]
    public void Resolve_AppliesTrackGain_InTrackMode()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Track, 0, null, BuildTrack(trackGain: -6, albumGain: -3), null);

        // Assert
        Assert.Equal(Linear(-6), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_applies_the_album_gain_in_album_mode")]
    public void Resolve_AppliesAlbumGain_InAlbumMode()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Album, 0, null, BuildTrack(trackGain: -6, albumGain: -3), null);

        // Assert
        Assert.Equal(Linear(-3), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_falls_back_to_the_track_gain_when_the_album_gain_is_missing")]
    public void Resolve_FallsBackToTrackGain_WhenAlbumGainIsMissing()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Album, 0, null, BuildTrack(trackGain: -6), null);

        // Assert
        Assert.Equal(Linear(-6), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_falls_back_to_the_album_gain_when_the_track_gain_is_missing")]
    public void Resolve_FallsBackToAlbumGain_WhenTrackGainIsMissing()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Track, 0, null, BuildTrack(albumGain: -3), null);

        // Assert
        Assert.Equal(Linear(-3), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_adds_the_preamp_to_the_gain")]
    public void Resolve_AddsPreampToGain()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Track, 2.5, null, BuildTrack(trackGain: -8), null);

        // Assert
        Assert.Equal(Linear(-5.5), gain, Tolerance);
    }

    [Theory(DisplayName = "resolve_clamps_the_preamp_to_the_allowed_range")]
    [InlineData(12, 6)]
    [InlineData(-20, -6)]
    public void Resolve_ClampsPreampToAllowedRange(double preamp, double effectivePreamp)
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Track, preamp, null, BuildTrack(trackGain: -10), null);

        // Assert
        Assert.Equal(Linear(-10 + effectivePreamp), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_caps_the_gain_at_the_inverse_of_the_peak")]
    public void Resolve_CapsGainAtInverseOfPeak()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Track, 6, null, BuildTrack(trackGain: 2, trackPeak: 0.9), null);

        // Assert
        Assert.Equal(1f / 0.9f, gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_caps_below_unity_when_the_peak_exceeds_full_scale")]
    public void Resolve_CapsBelowUnity_WhenPeakExceedsFullScale()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Track, 0, null, BuildTrack(trackGain: 0, trackPeak: 2.73), null);

        // Assert
        Assert.Equal(1f / 2.73f, gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_uses_the_peak_of_the_same_level_as_the_gain")]
    public void Resolve_UsesPeakOfSameLevelAsGain()
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Album, 0, null, BuildTrack(trackGain: 0, trackPeak: 4, albumGain: 0, albumPeak: 0.5), null);

        // Assert
        Assert.Equal(1f, gain, Tolerance);
    }

    [Theory(DisplayName = "resolve_ignores_an_unusable_peak")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Resolve_IgnoresUnusablePeak(double peak)
    {
        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Track, 0, null, BuildTrack(trackGain: 3, trackPeak: peak), null);

        // Assert
        Assert.Equal(Linear(3), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_in_auto_mode_uses_the_album_gain_when_the_next_track_follows")]
    public void Resolve_InAutoMode_UsesAlbumGain_WhenNextTrackFollows()
    {
        // Arrange
        TrackDto first = BuildTrack(trackGain: -6, albumGain: -3, albumId: 5, trackNumber: 1);
        TrackDto second = BuildTrack(albumId: 5, trackNumber: 2);

        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Auto, 0, null, first, second);

        // Assert
        Assert.Equal(Linear(-3), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_in_auto_mode_uses_the_album_gain_when_the_previous_track_precedes")]
    public void Resolve_InAutoMode_UsesAlbumGain_WhenPreviousTrackPrecedes()
    {
        // Arrange
        TrackDto previous = BuildTrack(albumId: 5, trackNumber: 3);
        TrackDto current = BuildTrack(trackGain: -6, albumGain: -3, albumId: 5, trackNumber: 4);

        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Auto, 0, previous, current, null);

        // Assert
        Assert.Equal(Linear(-3), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_in_auto_mode_uses_the_track_gain_between_unrelated_tracks")]
    public void Resolve_InAutoMode_UsesTrackGain_BetweenUnrelatedTracks()
    {
        // Arrange
        TrackDto previous = BuildTrack(albumId: 9, trackNumber: 3);
        TrackDto current = BuildTrack(trackGain: -6, albumGain: -3, albumId: 5, trackNumber: 4);
        TrackDto next = BuildTrack(albumId: 5, trackNumber: 7);

        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Auto, 0, previous, current, next);

        // Assert
        Assert.Equal(Linear(-6), gain, Tolerance);
    }

    [Fact(DisplayName = "resolve_in_auto_mode_uses_the_track_gain_when_album_or_number_is_missing")]
    public void Resolve_InAutoMode_UsesTrackGain_WhenAlbumOrNumberIsMissing()
    {
        // Arrange
        TrackDto current = BuildTrack(trackGain: -6, albumGain: -3);
        TrackDto next = BuildTrack(trackNumber: 1);

        // Act
        float gain = ReplayGainCalculator.Resolve(EReplayGainMode.Auto, 0, null, current, next);

        // Assert
        Assert.Equal(Linear(-6), gain, Tolerance);
    }
}