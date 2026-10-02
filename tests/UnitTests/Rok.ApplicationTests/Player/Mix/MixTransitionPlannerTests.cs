using Rok.Application.Dto;
using Rok.Application.Player;
using Rok.Application.Player.Mix;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Player.Mix;

public class MixTransitionPlannerTests
{
    private static readonly TrackDto Current = new() { Id = 1 };
    private static readonly TrackDto Next = new() { Id = 2 };

    private static MixPlan? Plan(double musicEnd, double fadeOut, double length, int slider, double introStart = 0, BeatGrid? outBeats = null, BeatGrid? inBeats = null)
    {
        return MixTransitionPlanner.Plan(Current, new OutroCues(musicEnd, fadeOut, outBeats), Next, new IntroCues(introStart, inBeats), length, slider);
    }

    private static BeatGrid Grid(double bpm, double first) => new(bpm, first, 0.9, BpmSource.Detected);

    private static bool OnBeat(double time, double first, double bpm)
    {
        var period = 60 / bpm;
        var ratio = (time - first) / period;

        return Math.Abs(ratio - Math.Round(ratio)) < 0.01 / period;
    }

    [Fact(DisplayName = "plan_ends_the_fade_on_the_music_end")]
    public void Plan_EndsTheFadeOnTheMusicEnd()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(200, plan.StartSeconds + plan.DurationSeconds, 6);
        Assert.Equal(1, plan.OutgoingTrackId);
        Assert.Equal(2, plan.IncomingTrackId);
    }

    [Theory(DisplayName = "plan_never_exceeds_the_slider_duration")]
    [InlineData(1, 0)]
    [InlineData(5, 3)]
    [InlineData(5, 20)]
    [InlineData(12, 0)]
    [InlineData(12, 20)]
    [InlineData(0, 10)]
    [InlineData(40, 0)]
    public void Plan_NeverExceedsTheSliderDuration(int slider, double fadeOut)
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut, length: 210, slider);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.DurationSeconds <= CrossfadeDuration.Clamp(slider));
    }

    [Fact(DisplayName = "plan_uses_the_natural_fade_when_shorter_than_the_slider")]
    public void Plan_NaturalFadeShorterThanSlider_UsesTheNaturalFade()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 4, length: 210, slider: 10);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(4, plan.DurationSeconds, 6);
    }

    [Fact(DisplayName = "plan_uses_the_slider_when_the_ending_is_abrupt")]
    public void Plan_AbruptEnding_UsesTheSliderDuration()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 7);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(7, plan.DurationSeconds, 6);
    }

    [Theory(DisplayName = "plan_stays_in_the_last_30_seconds")]
    [InlineData(200, 0, 210, 12)]
    [InlineData(100, 20, 210, 12)]
    [InlineData(185, 0, 210, 12)]
    [InlineData(210, 0, 210, 12)]
    [InlineData(150, 0, 150, 12)]
    [InlineData(40, 0, 40, 12)]
    public void Plan_StaysInTheLast30Seconds(double musicEnd, double fadeOut, double length, int slider)
    {
        // Arrange & Act
        var plan = Plan(musicEnd, fadeOut, length, slider);

        // Assert
        if (plan is not null)
        {
            Assert.True(plan.StartSeconds >= length - 30);
        }
    }

    [Fact(DisplayName = "plan_starts_the_incoming_track_at_its_music_start")]
    public void Plan_StartsTheIncomingTrackAtItsMusicStart()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 5, introStart: 2.9);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(2.9, plan.IncomingStartSeconds, 6);
    }

    [Fact(DisplayName = "plan_is_null_when_too_short_to_mix")]
    public void Plan_MusicEndsBeforeTheWindow_ReturnsNull()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 180.5, fadeOut: 0, length: 210, slider: 5);

        // Assert
        Assert.Null(plan);
    }

    [Fact(DisplayName = "plan_is_capped_to_half_the_track_length")]
    public void Plan_ShortTrack_CapsTheDurationToHalfTheLength()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 6, fadeOut: 0, length: 6, slider: 12);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(3, plan.DurationSeconds, 6);
    }

    [Fact(DisplayName = "plan_shrinks_the_duration_when_start_is_moved_to_the_window")]
    public void Plan_StartBeforeTheWindow_ShrinksTheDuration()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 185, fadeOut: 0, length: 210, slider: 12);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(180, plan.StartSeconds, 6);
        Assert.Equal(5, plan.DurationSeconds, 6);
    }

    [Fact(DisplayName = "resolve_at_keeps_the_plan_when_on_time")]
    public void ResolveAt_OnTime_KeepsThePlan()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.5)!;

        // Act
        var (duration, incomingStart, _) = MixTransitionPlanner.ResolveAt(plan, plan.StartSeconds + 0.2);

        // Assert
        Assert.Equal(plan.DurationSeconds, duration);
        Assert.Equal(1.5, incomingStart);
    }

    [Fact(DisplayName = "resolve_at_shortens_a_late_mix")]
    public void ResolveAt_Late_ShortensTheMix()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6)!;

        // Act
        var (duration, _, _) = MixTransitionPlanner.ResolveAt(plan, 197);

        // Assert
        Assert.Equal(3, duration, 6);
    }

    [Fact(DisplayName = "resolve_at_never_goes_below_the_minimum")]
    public void ResolveAt_AfterTheMusicEnd_UsesTheMinimumDuration()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6)!;

        // Act
        var (duration, _, _) = MixTransitionPlanner.ResolveAt(plan, 205);

        // Assert
        Assert.Equal(MixThresholds.MinMixSeconds, duration);
    }

    [Fact(DisplayName = "resolve_at_never_exceeds_the_planned_duration")]
    public void ResolveAt_JustLate_NeverExceedsThePlannedDuration()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6)!;

        // Act
        var (duration, _, _) = MixTransitionPlanner.ResolveAt(plan, plan.StartSeconds + 0.5);

        // Assert
        Assert.Equal(plan.DurationSeconds - 0.5, duration, 6);
    }

    [Fact(DisplayName = "aligned_mix_starts_on_an_outgoing_beat")]
    public void Plan_WithCompatibleGrids_StartsOnAnOutgoingBeat()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.2));

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Alignment);
        Assert.True(OnBeat(plan.StartSeconds, 170.13, 128));
    }

    [Fact(DisplayName = "aligned_incoming_starts_on_its_first_beat_from_music_start")]
    public void Plan_WithCompatibleGrids_StartsTheIncomingOnItsFirstBeat()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.2, outBeats: Grid(120, 170.13), inBeats: Grid(120, 0.31));

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(1.31, plan.IncomingStartSeconds, 2);
        Assert.True(plan.IncomingStartSeconds >= 1.2);
        Assert.True(plan.IncomingStartSeconds < 1.2 + 0.5);
    }

    [Fact(DisplayName = "aligned_bass_swap_falls_on_an_outgoing_beat")]
    public void Plan_WithCompatibleGrids_SwapsTheBassOnAnOutgoingBeat()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.2));

        // Assert
        Assert.NotNull(plan);
        Assert.True(OnBeat(plan.StartSeconds + plan.BassSwapAtSeconds, 170.13, 128));
        Assert.InRange(plan.BassSwapAtSeconds, BassSwapCurve.RampSeconds / 2, plan.DurationSeconds - (BassSwapCurve.RampSeconds / 2));
    }

    [Theory(DisplayName = "aligned_shift_is_under_one_beat_and_keeps_the_limits")]
    [InlineData(87, 0, 4)]
    [InlineData(87, 0.13, 12)]
    [InlineData(87, 0.5, 4)]
    [InlineData(87, 0.97, 12)]
    [InlineData(120, 0, 12)]
    [InlineData(120, 0.13, 4)]
    [InlineData(120, 0.5, 12)]
    [InlineData(120, 0.97, 4)]
    [InlineData(128, 0, 4)]
    [InlineData(128, 0.13, 12)]
    [InlineData(128, 0.5, 4)]
    [InlineData(128, 0.97, 12)]
    [InlineData(174, 0, 12)]
    [InlineData(174, 0.13, 4)]
    [InlineData(174, 0.5, 12)]
    [InlineData(174, 0.97, 4)]
    public void Plan_Aligned_KeepsTheLimits(double bpm, double phase, int slider)
    {
        // Arrange
        var period = 60 / bpm;
        var first = 170 + (phase * period);
        var unaligned = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider)!;

        // Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider, outBeats: Grid(bpm, first), inBeats: Grid(bpm, 0.1))!;

        // Assert
        Assert.NotNull(plan.Alignment);

        var shift = unaligned.StartSeconds - plan.StartSeconds;

        Assert.InRange(shift, 0, period - 1e-9);
        Assert.True(plan.StartSeconds >= 210 - 30);
        Assert.True(plan.DurationSeconds <= CrossfadeDuration.Clamp(slider));
        Assert.Equal(unaligned.DurationSeconds, plan.DurationSeconds);
    }

    [Fact(DisplayName = "plan_without_outro_grid_is_unchanged")]
    public void Plan_WithoutOutroGrid_IsUnchanged()
    {
        // Arrange
        var expected = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.5);

        // Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.5, inBeats: Grid(120, 0.3));

        // Assert
        Assert.Equal(expected, plan);
        Assert.Equal(plan!.DurationSeconds / 2, plan.BassSwapAtSeconds);
        Assert.Null(plan.Alignment);
    }

    [Fact(DisplayName = "plan_without_intro_grid_is_unchanged")]
    public void Plan_WithoutIntroGrid_IsUnchanged()
    {
        // Arrange
        var expected = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.5);

        // Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.5, outBeats: Grid(120, 170.3));

        // Assert
        Assert.Equal(expected, plan);
        Assert.Equal(plan!.DurationSeconds / 2, plan.BassSwapAtSeconds);
        Assert.Null(plan.Alignment);
    }

    [Fact(DisplayName = "plan_with_incompatible_tempos_is_unchanged")]
    public void Plan_WithIncompatibleTempos_IsUnchanged()
    {
        // Arrange
        var expected = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6);

        // Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(120, 170.3), inBeats: Grid(128, 0.2));

        // Assert
        Assert.Equal(expected, plan);
    }

    [Fact(DisplayName = "plan_with_octave_tempos_is_aligned")]
    public void Plan_WithOctaveTempos_IsAligned()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(87, 170.3), inBeats: Grid(174, 0.2));

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Alignment);
    }

    [Fact(DisplayName = "plan_is_not_aligned_when_the_previous_beat_leaves_the_window")]
    public void Plan_PreviousBeatBeforeTheWindow_IsUnchanged()
    {
        // Arrange
        var expected = Plan(musicEnd: 185, fadeOut: 0, length: 210, slider: 12);

        // Act
        var plan = Plan(musicEnd: 185, fadeOut: 0, length: 210, slider: 12, outBeats: Grid(128, 181), inBeats: Grid(128, 0.2));

        // Assert
        Assert.Equal(expected, plan);
        Assert.Null(plan!.Alignment);
    }

    [Fact(DisplayName = "resolve_at_on_time_keeps_the_aligned_plan")]
    public void ResolveAt_AlignedOnTime_KeepsThePlan()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.2, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.31))!;

        // Act
        var (duration, incomingStart, bassSwapAt) = MixTransitionPlanner.ResolveAt(plan, plan.StartSeconds);

        // Assert
        Assert.Equal(plan.DurationSeconds, duration);
        Assert.Equal(plan.IncomingStartSeconds, incomingStart);
        Assert.Equal(plan.BassSwapAtSeconds, bassSwapAt);
    }

    [Fact(DisplayName = "resolve_at_shifts_the_incoming_start_by_the_lateness_phase")]
    public void ResolveAt_AlignedLate_ShiftsTheIncomingStartByThePhase()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.2, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.31))!;
        var period = 60.0 / 128;

        // Act
        var (_, incomingStart, _) = MixTransitionPlanner.ResolveAt(plan, plan.StartSeconds + 0.2);

        // Assert
        Assert.Equal(plan.IncomingStartSeconds + (0.2 % period), incomingStart, 6);
    }

    [Fact(DisplayName = "resolve_at_keeps_the_bass_swap_on_an_outgoing_beat_when_late")]
    public void ResolveAt_AlignedLate_KeepsTheBassSwapOnABeat()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.31))!;
        var position = plan.StartSeconds + 1.3;

        // Act
        var (duration, _, bassSwapAt) = MixTransitionPlanner.ResolveAt(plan, position);

        // Assert
        Assert.True(OnBeat(position + bassSwapAt, 170.13, 128));
        Assert.InRange(bassSwapAt, BassSwapCurve.RampSeconds / 2, duration - (BassSwapCurve.RampSeconds / 2));
    }

    [Fact(DisplayName = "resolve_at_unaligned_uses_the_middle_of_the_resolved_mix")]
    public void ResolveAt_Unaligned_UsesTheMiddleOfTheResolvedMix()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.5)!;

        // Act
        var (_, incomingStart, bassSwapAt) = MixTransitionPlanner.ResolveAt(plan, 197);

        // Assert
        Assert.Equal(1.5, bassSwapAt, 6);
        Assert.Equal(1.5, incomingStart);
    }

    [Fact(DisplayName = "resolve_at_falls_back_to_the_middle_when_no_beat_fits")]
    public void ResolveAt_AlignedTooLate_FallsBackToTheMiddle()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.31))!;

        // Act
        var (duration, _, bassSwapAt) = MixTransitionPlanner.ResolveAt(plan, 205);

        // Assert
        Assert.Equal(MixThresholds.MinMixSeconds, duration);
        Assert.Equal(duration / 2, bassSwapAt);
    }

    [Fact(DisplayName = "unaligned_bass_swap_is_the_middle")]
    public void Plan_WithoutGrid_SwapsTheBassAtTheMiddle()
    {
        // Arrange & Act
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 7);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(plan.DurationSeconds / 2, plan.BassSwapAtSeconds);
        Assert.Null(plan.Alignment);
    }
}