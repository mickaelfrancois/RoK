using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player.Mix;

public class MixStartCompensationTests
{
    [Fact(DisplayName = "compensation_skips_the_open_time_scaled_by_the_ratio")]
    public void Compute_WithLag_SkipsScaledAndShortensTheMix()
    {
        // Act
        var (skip, duration, swap) = MixStartCompensation.Compute(100, 100.3, 1.06, 15, 7.5);

        // Assert
        Assert.Equal(0.318, skip, 6);
        Assert.Equal(14.7, duration, 6);
        Assert.Equal(7.2, swap, 6);
    }

    [Fact(DisplayName = "compensation_without_reference_is_neutral")]
    public void Compute_WithoutReference_IsNeutral()
    {
        // Act
        var result = MixStartCompensation.Compute(null, 100.3, 1.06, 15, 7.5);

        // Assert
        Assert.Equal((0d, 15d, 7.5d), result);
    }

    [Fact(DisplayName = "negative_lag_is_ignored")]
    public void Compute_NegativeLag_IsNeutral()
    {
        // Act
        var result = MixStartCompensation.Compute(100, 99.5, 1.06, 15, 7.5);

        // Assert
        Assert.Equal((0d, 15d, 7.5d), result);
    }

    [Fact(DisplayName = "lag_never_leaves_less_than_the_minimum_mix")]
    public void Compute_HugeLag_KeepsTheMinimumMix()
    {
        // Act
        var (skip, duration, _) = MixStartCompensation.Compute(100, 150, 1.0, 15, 7.5);

        // Assert
        Assert.Equal(MixThresholds.MinMixSeconds, duration, 9);
        Assert.Equal(15 - MixThresholds.MinMixSeconds, skip, 9);
    }
}