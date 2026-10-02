using Rok.Application.Dto;
using Rok.Application.Player;
using Rok.Application.Player.Mix;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Player.Mix;

public class MixTransitionPlannerTests
{
    private static readonly TrackDto Current = new() { Id = 1 };
    private static readonly TrackDto Next = new() { Id = 2 };

    // Too short to be stretched over 4 bars: keeps the plans of the non-stretched mix (#437, #444).
    private const long ShortIncoming = 30;

    private static TrackDto NextTrack(long duration) => new() { Id = 2, Duration = duration };

    private static MixPlan? Plan(double musicEnd, double fadeOut, double length, int slider, double introStart = 0, BeatGrid? outBeats = null, BeatGrid? inBeats = null, long incomingDuration = 0)
    {
        return MixTransitionPlanner.Plan(Current, new OutroCues(musicEnd, fadeOut, outBeats), NextTrack(incomingDuration), new IntroCues(introStart, inBeats), length, slider);
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
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider, outBeats: Grid(bpm, first), inBeats: Grid(bpm, 0.1), incomingDuration: ShortIncoming)!;

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
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(120, 170.3), inBeats: Grid(140, 0.2));

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
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.2, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.31), incomingDuration: ShortIncoming)!;

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
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, introStart: 1.2, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.31), incomingDuration: ShortIncoming)!;
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
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.31), incomingDuration: ShortIncoming)!;
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
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6, outBeats: Grid(128, 170.13), inBeats: Grid(128, 0.31), incomingDuration: ShortIncoming)!;

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

    private const double OutDownbeat = 170;
    private const double MixPointSeconds = 186;
    private const double Bar120 = 2;

    private static BeatGrid GridWithDownbeat(double bpm, double first, double downbeat) => new(bpm, first, 0.9, BpmSource.Detected, downbeat);

    private static MixPlan? PlanWithPoint(
        MixPoint? point,
        BeatGrid? outBeats,
        BeatGrid? inBeats,
        int slider = 6,
        double musicEnd = 200,
        double length = 210,
        double introStart = 1.2,
        long incomingDuration = ShortIncoming)
    {
        return MixTransitionPlanner.Plan(Current, new OutroCues(musicEnd, 0, outBeats, point), NextTrack(incomingDuration), new IntroCues(introStart, inBeats), length, slider);
    }

    private static BeatGrid BarOut => GridWithDownbeat(120, OutDownbeat, OutDownbeat);

    private static BeatGrid BarIn => GridWithDownbeat(120, 0.31, 0.81);

    private static bool OnBar(double time, double downbeat, double barPeriod)
    {
        var ratio = (time - downbeat) / barPeriod;

        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }

    [Theory(DisplayName = "plan_below_the_threshold_is_identical_to_the_437_plan")]
    [InlineData(0.0, true)]
    [InlineData(0.44, true)]
    [InlineData(0.0, false)]
    public void Plan_MixPointBelowTheThreshold_IsIdenticalToThe437Plan(double score, bool withPoint)
    {
        // Arrange
        var expected = PlanWithPoint(null, BarOut, BarIn);
        MixPoint? point = withPoint ? new MixPoint(MixPointSeconds, score) : null;

        // Act
        var plan = PlanWithPoint(point, BarOut, BarIn);

        // Assert
        Assert.True(score < MixThresholds.MinMixPointScore);
        Assert.Equal(expected, plan);
        Assert.Null(plan!.MixPointScore);
    }

    [Theory(DisplayName = "mix_point_plan_stays_in_the_last_30_seconds_and_never_exceeds_the_slider")]
    [InlineData(1, 170)]
    [InlineData(1, 186)]
    [InlineData(1, 198)]
    [InlineData(6, 170)]
    [InlineData(6, 186)]
    [InlineData(6, 198)]
    [InlineData(12, 170)]
    [InlineData(12, 186)]
    [InlineData(12, 198)]
    public void Plan_MixPoint_StaysInTheWindowAndNeverExceedsTheSlider(int slider, double pointSeconds)
    {
        // Arrange
        var fallback = PlanWithPoint(null, BarOut, BarIn, slider);

        // Act
        var plan = PlanWithPoint(new MixPoint(pointSeconds, 0.9), BarOut, BarIn, slider);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.StartSeconds >= 210 - 30);
        Assert.True(plan.DurationSeconds <= CrossfadeDuration.Clamp(slider));
        Assert.True(plan.StartSeconds + plan.DurationSeconds <= 200 + 1e-9);

        if (pointSeconds < 180)
        {
            Assert.Equal(fallback, plan);
        }
        else
        {
            Assert.Equal(pointSeconds, plan.StartSeconds);
            Assert.Equal(0.9, plan.MixPointScore);
        }
    }

    [Fact(DisplayName = "mix_point_plan_starts_the_incoming_track_on_its_first_downbeat")]
    public void Plan_MixPoint_StartsTheIncomingOnItsFirstDownbeat()
    {
        // Arrange & Act
        var plan = PlanWithPoint(new MixPoint(MixPointSeconds, 0.9), BarOut, BarIn);

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Alignment);
        Assert.True(plan.Alignment.BarAligned);
        Assert.Equal(2.81, plan.IncomingStartSeconds, 2);
        Assert.True(plan.IncomingStartSeconds >= 1.2);
    }

    [Fact(DisplayName = "mix_point_bass_swap_falls_on_an_outgoing_bar")]
    public void Plan_MixPoint_SwapsTheBassOnAnOutgoingBar()
    {
        // Arrange & Act
        var plan = PlanWithPoint(new MixPoint(MixPointSeconds, 0.9), BarOut, BarIn);

        // Assert
        Assert.NotNull(plan);
        Assert.True(OnBar(plan.StartSeconds + plan.BassSwapAtSeconds, OutDownbeat, Bar120));
        Assert.InRange(plan.BassSwapAtSeconds, BassSwapCurve.RampSeconds / 2, plan.DurationSeconds - (BassSwapCurve.RampSeconds / 2));
    }

    [Fact(DisplayName = "mix_point_plan_without_incoming_downbeat_is_beat_aligned")]
    public void Plan_MixPointWithoutIncomingDownbeat_IsBeatAligned()
    {
        // Arrange & Act
        var plan = PlanWithPoint(new MixPoint(MixPointSeconds, 0.9), BarOut, Grid(120, 0.31));

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Alignment);
        Assert.False(plan.Alignment.BarAligned);
        Assert.True(OnBeat(plan.IncomingStartSeconds, 0.31, 120));
        Assert.True(plan.IncomingStartSeconds >= 1.2);
        Assert.True(OnBeat(plan.StartSeconds + plan.BassSwapAtSeconds, OutDownbeat, 120));
    }

    [Fact(DisplayName = "mix_point_with_incompatible_tempos_is_used_unaligned")]
    public void Plan_MixPointWithIncompatibleTempos_IsUsedUnaligned()
    {
        // Arrange & Act
        var plan = PlanWithPoint(new MixPoint(MixPointSeconds, 0.9), BarOut, GridWithDownbeat(140, 0.2, 0.2));

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(MixPointSeconds, plan.StartSeconds);
        Assert.Null(plan.Alignment);
        Assert.Equal(1.2, plan.IncomingStartSeconds);
        Assert.Equal(plan.DurationSeconds / 2, plan.BassSwapAtSeconds);
        Assert.Equal(0.9, plan.MixPointScore);
    }

    [Fact(DisplayName = "plan_with_octave_tempos_is_bar_aligned_on_the_outgoing_bar")]
    public void Plan_MixPointWithOctaveTempos_IsBarAlignedOnTheOutgoingBar()
    {
        // Arrange
        var outBeats = GridWithDownbeat(87, 170.3, 170.3);
        var outBar = outBeats.BarPeriodSeconds;
        var point = new MixPoint(170.3 + (5 * outBar), 0.9);

        // Act
        var plan = PlanWithPoint(point, outBeats, GridWithDownbeat(174, 0.2, 0.2));

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Alignment);
        Assert.True(plan.Alignment.BarAligned);
        Assert.True(OnBar(plan.StartSeconds + plan.BassSwapAtSeconds, 170.3, outBar));
    }

    [Fact(DisplayName = "resolve_at_keeps_the_bar_phase_when_late")]
    public void ResolveAt_BarAlignedLate_KeepsTheBarPhase()
    {
        // Arrange
        var plan = PlanWithPoint(new MixPoint(MixPointSeconds, 0.9), BarOut, BarIn)!;
        var lateness = 1.3 * Bar120;
        var position = plan.StartSeconds + lateness;

        // Act
        var (duration, incomingStart, bassSwapAt) = MixTransitionPlanner.ResolveAt(plan, position);

        // Assert
        Assert.Equal(plan.IncomingStartSeconds + (lateness % Bar120), incomingStart, 6);
        Assert.True(OnBar(position + bassSwapAt, OutDownbeat, Bar120));
        Assert.InRange(bassSwapAt, BassSwapCurve.RampSeconds / 2, duration - (BassSwapCurve.RampSeconds / 2));
    }

    [Theory(DisplayName = "planned_bass_swap_is_kept_by_the_engine_rule")]
    [InlineData("bar")]
    [InlineData("beat")]
    [InlineData("none")]
    [InlineData("octave")]
    public void Plan_BassSwap_IsKeptByTheEngineRule(string mode)
    {
        // Arrange
        var inBeats = mode switch
        {
            "bar" => BarIn,
            "beat" => Grid(120, 0.31),
            "octave" => GridWithDownbeat(240, 0.2, 0.2),
            _ => GridWithDownbeat(140, 0.2, 0.2)
        };

        // Act
        var plan = PlanWithPoint(new MixPoint(MixPointSeconds, 0.9), BarOut, inBeats)!;

        // Assert
        Assert.True(BassSwapCurve.Applies(plan.DurationSeconds));
        Assert.Equal(plan.BassSwapAtSeconds, BassSwapCurve.ResolveSwapAt(plan.BassSwapAtSeconds, plan.DurationSeconds));
    }

    private const double Bar128 = 1.875;

    private static BeatGrid StretchOut => GridWithDownbeat(128, OutDownbeat, OutDownbeat);

    private static BeatGrid StretchIn(double ratio) => GridWithDownbeat(128 / ratio, 0.31, 0.81);

    private static MixPlan? PlanStretched(
        BeatGrid? outBeats,
        BeatGrid? inBeats,
        MixPoint? point = null,
        int slider = 6,
        double musicEnd = 200,
        double length = 210,
        long incomingDuration = 0)
    {
        return PlanWithPoint(point, outBeats, inBeats, slider, musicEnd, length, introStart: 0, incomingDuration);
    }

    [Fact(DisplayName = "tempos_seven_percent_apart_are_stretched_over_eight_bars")]
    public void Plan_TemposSevenPercentApart_AreStretchedOverEightBars()
    {
        // Arrange & Act
        var plan = PlanStretched(StretchOut, StretchIn(1.07));

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Stretch);
        Assert.Equal(8, plan.Stretch.Bars);
        Assert.Equal(1.07, plan.Stretch.Ratio, 9);
        Assert.Equal(8 * Bar128, plan.DurationSeconds, 9);
        Assert.True(OnBar(plan.StartSeconds, OutDownbeat, Bar128));
        Assert.True(OnBar(plan.StartSeconds + plan.DurationSeconds, OutDownbeat, Bar128));
        Assert.NotNull(plan.Alignment);
        Assert.True(plan.Alignment.BarAligned);
    }

    [Fact(DisplayName = "tempos_nine_percent_apart_keep_the_current_plan")]
    public void Plan_TemposNinePercentApart_KeepTheCurrentPlan()
    {
        // Arrange
        var expected = PlanStretched(null, null);

        // Act
        var plan = PlanStretched(StretchOut, StretchIn(1.09));

        // Assert
        Assert.NotNull(plan);
        Assert.Null(plan.Stretch);
        Assert.Equal(expected, plan);
    }

    [Theory(DisplayName = "stretched_plan_falls_back_to_four_bars_then_to_the_current_plan")]
    [InlineData(200, 0, 8)]
    [InlineData(200, 100, 8)]
    [InlineData(200, 50, 4)]
    [InlineData(200, 30, 0)]
    [InlineData(190, 0, 4)]
    [InlineData(185, 0, 0)]
    public void Plan_Stretched_FallsBackToFourBarsThenToTheCurrentPlan(double musicEnd, long incomingDuration, int expectedBars)
    {
        // Arrange & Act
        var plan = PlanStretched(StretchOut, StretchIn(1), musicEnd: musicEnd, incomingDuration: incomingDuration);

        // Assert
        if (expectedBars == 0)
        {
            Assert.True(plan is null || plan.Stretch is null);

            return;
        }

        Assert.NotNull(plan);
        Assert.NotNull(plan.Stretch);
        Assert.Equal(expectedBars, plan.Stretch.Bars);
        Assert.Equal(expectedBars * Bar128, plan.DurationSeconds, 9);
    }

    [Fact(DisplayName = "stretched_plan_starts_on_the_mix_point_when_it_fits")]
    public void Plan_Stretched_StartsOnTheMixPointWhenItFits()
    {
        // Arrange & Act
        var plan = PlanStretched(StretchOut, StretchIn(1.05), new MixPoint(184, 0.9));

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Stretch);
        Assert.Equal(184, plan.StartSeconds);
        Assert.Equal(0.9, plan.MixPointScore);
        Assert.Equal(8, plan.Stretch.Bars);
    }

    [Fact(DisplayName = "mix_point_too_late_for_eight_bars_uses_the_bar_grid")]
    public void Plan_Stretched_MixPointTooLateForEightBars_UsesTheBarGrid()
    {
        // Arrange & Act
        var plan = PlanStretched(StretchOut, StretchIn(1.05), new MixPoint(190, 0.9));

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Stretch);
        Assert.Equal(8, plan.Stretch.Bars);
        Assert.Equal(185, plan.StartSeconds, 9);
        Assert.Null(plan.MixPointScore);
    }

    [Theory(DisplayName = "stretched_plan_stays_in_the_last_30_seconds_and_ignores_the_slider")]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Plan_Stretched_StaysInTheWindowAndIgnoresTheSlider(int slider)
    {
        // Arrange & Act
        var plan = PlanStretched(StretchOut, StretchIn(1.05), slider: slider);

        // Assert
        Assert.NotNull(plan);
        Assert.NotNull(plan.Stretch);
        Assert.True(plan.StartSeconds >= 210 - 30);
        Assert.Equal(8 * Bar128, plan.DurationSeconds, 9);
    }

    [Fact(DisplayName = "stretched_bass_swap_falls_on_the_middle_bar")]
    public void Plan_Stretched_SwapsTheBassOnTheMiddleBar()
    {
        // Arrange & Act
        var plan = PlanStretched(StretchOut, StretchIn(1.05));

        // Assert
        Assert.NotNull(plan);
        Assert.Equal(4 * Bar128, plan.BassSwapAtSeconds, 9);
    }

    [Fact(DisplayName = "resolve_at_keeps_the_stretched_end_on_the_bar_when_late")]
    public void ResolveAt_StretchedLate_KeepsTheEndOnTheBar()
    {
        // Arrange
        var plan = PlanStretched(StretchOut, StretchIn(1.05), new MixPoint(184, 0.9))!;
        var position = plan.StartSeconds + 3;

        // Act
        var (duration, _, _) = MixTransitionPlanner.ResolveAt(plan, position);

        // Assert
        Assert.True(plan.MusicEndSeconds > plan.StartSeconds + plan.DurationSeconds - 1e-9);
        Assert.Equal(plan.StartSeconds + plan.DurationSeconds, position + duration, 9);
    }

    [Fact(DisplayName = "resolve_at_scales_the_incoming_shift_by_the_ratio")]
    public void ResolveAt_StretchedLate_ScalesTheIncomingShiftByTheRatio()
    {
        // Arrange
        var plan = PlanStretched(StretchOut, StretchIn(1.05))!;
        var lateness = 4.0;

        // Act
        var (_, incomingStart, _) = MixTransitionPlanner.ResolveAt(plan, plan.StartSeconds + lateness);

        // Assert
        Assert.Equal(plan.IncomingStartSeconds + ((lateness % Bar128) * plan.Stretch!.Ratio), incomingStart, 9);
    }

    [Theory(DisplayName = "planned_stretch_is_kept_by_the_engine_rules")]
    [InlineData(1.07)]
    [InlineData(0.93)]
    [InlineData(1.0)]
    [InlineData(1.0799)]
    public void Plan_Stretch_IsKeptByTheEngineRules(double ratio)
    {
        // Arrange & Act
        var plan = PlanStretched(StretchOut, StretchIn(ratio))!;

        // Assert
        var stretch = plan.Stretch!;

        Assert.Equal(stretch.Ratio, TempoStretchCurve.ClampRatio(stretch.Ratio));
        Assert.Equal(stretch.Ratio, TempoStretchCurve.TempoAt(stretch.Ratio, plan.DurationSeconds, stretch.ReturnSeconds, plan.DurationSeconds - 1e-6));
        Assert.Equal(1, TempoStretchCurve.TempoAt(stretch.Ratio, plan.DurationSeconds, stretch.ReturnSeconds, plan.DurationSeconds + stretch.ReturnSeconds));
    }
}