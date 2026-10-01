using Rok.Application.Dto;
using Rok.Application.Player;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player.Mix;

public class MixTransitionPlannerTests
{
    private static readonly TrackDto Current = new() { Id = 1 };
    private static readonly TrackDto Next = new() { Id = 2 };

    private static MixPlan? Plan(double musicEnd, double fadeOut, double length, int slider, double introStart = 0)
    {
        return MixTransitionPlanner.Plan(Current, new OutroCues(musicEnd, fadeOut), Next, new IntroCues(introStart), length, slider);
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
        var (duration, incomingStart) = MixTransitionPlanner.ResolveAt(plan, plan.StartSeconds + 0.2);

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
        var (duration, _) = MixTransitionPlanner.ResolveAt(plan, 197);

        // Assert
        Assert.Equal(3, duration, 6);
    }

    [Fact(DisplayName = "resolve_at_never_goes_below_the_minimum")]
    public void ResolveAt_AfterTheMusicEnd_UsesTheMinimumDuration()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6)!;

        // Act
        var (duration, _) = MixTransitionPlanner.ResolveAt(plan, 205);

        // Assert
        Assert.Equal(MixThresholds.MinMixSeconds, duration);
    }

    [Fact(DisplayName = "resolve_at_never_exceeds_the_planned_duration")]
    public void ResolveAt_JustLate_NeverExceedsThePlannedDuration()
    {
        // Arrange
        var plan = Plan(musicEnd: 200, fadeOut: 0, length: 210, slider: 6)!;

        // Act
        var (duration, _) = MixTransitionPlanner.ResolveAt(plan, plan.StartSeconds + 0.5);

        // Assert
        Assert.Equal(plan.DurationSeconds - 0.5, duration, 6);
    }
}