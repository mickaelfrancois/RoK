using Rok.ViewModels.Player;

namespace Rok.PresentationTests.ViewModels.Player;

public class VolumeWheelAccumulatorTests
{
    private readonly VolumeWheelAccumulator _sut = new();

    [Fact(DisplayName = "when_one_notch_up_volume_increases_by_five")]
    public void Apply_OneNotchUp_AddsFive()
    {
        var result = _sut.Apply(50, 120);

        Assert.Equal(55, result);
    }

    [Fact(DisplayName = "when_one_notch_down_volume_decreases_by_five")]
    public void Apply_OneNotchDown_SubtractsFive()
    {
        var result = _sut.Apply(50, -120);

        Assert.Equal(45, result);
    }

    [Fact(DisplayName = "when_delta_is_below_a_notch_volume_is_unchanged")]
    public void Apply_SubNotchDelta_KeepsVolume()
    {
        var result = _sut.Apply(50, 30);

        Assert.Equal(50, result);
    }

    [Fact(DisplayName = "when_sub_notch_deltas_add_up_a_step_is_applied")]
    public void Apply_SubNotchDeltasAccumulate_AppliesStepOnceReached()
    {
        _sut.Apply(50, 60);
        _sut.Apply(50, 40);

        var result = _sut.Apply(50, 20);

        Assert.Equal(55, result);
    }

    [Fact(DisplayName = "when_delta_exceeds_a_notch_the_remainder_is_kept")]
    public void Apply_DeltaAboveNotch_KeepsRemainder()
    {
        _sut.Apply(50, 180);

        var result = _sut.Apply(55, 60);

        Assert.Equal(60, result);
    }

    [Fact(DisplayName = "when_several_notches_in_one_delta_all_are_applied")]
    public void Apply_MultipleNotches_AppliesAll()
    {
        var result = _sut.Apply(50, 360);

        Assert.Equal(65, result);
    }

    [Fact(DisplayName = "when_direction_reverses_the_remainder_is_reset")]
    public void Apply_DirectionReverses_ResetsRemainder()
    {
        _sut.Apply(50, 100);

        var result = _sut.Apply(50, -100);

        Assert.Equal(50, result);
    }

    [Fact(DisplayName = "when_volume_is_fractional_the_step_keeps_the_fraction")]
    public void Apply_FractionalVolume_KeepsFraction()
    {
        var result = _sut.Apply(47.4, 120);

        Assert.Equal(52.4, result, 6);
    }

    [Theory(DisplayName = "when_volume_would_exceed_100_it_is_clamped")]
    [InlineData(98)]
    [InlineData(100)]
    public void Apply_AboveMaximum_ClampsTo100(double current)
    {
        var result = _sut.Apply(current, 120);

        Assert.Equal(100, result);
    }

    [Theory(DisplayName = "when_volume_would_go_below_0_it_is_clamped")]
    [InlineData(2)]
    [InlineData(0)]
    public void Apply_BelowMinimum_ClampsTo0(double current)
    {
        var result = _sut.Apply(current, -120);

        Assert.Equal(0, result);
    }

    [Fact(DisplayName = "when_delta_is_zero_volume_is_unchanged")]
    public void Apply_ZeroDelta_KeepsVolume()
    {
        var result = _sut.Apply(40, 0);

        Assert.Equal(40, result);
    }

    [Theory(DisplayName = "when_converting_volume_to_percent_it_rounds_away_from_zero_within_bounds")]
    [InlineData(0, 0)]
    [InlineData(49.5, 50)]
    [InlineData(49.4, 49)]
    [InlineData(100, 100)]
    [InlineData(120, 100)]
    [InlineData(-5, 0)]
    public void ToPercent_Volume_ReturnsRoundedClampedPercent(double volume, int expected)
    {
        var result = VolumeWheelAccumulator.ToPercent(volume);

        Assert.Equal(expected, result);
    }
}