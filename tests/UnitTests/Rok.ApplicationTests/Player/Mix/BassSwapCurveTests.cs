using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player.Mix;

public class BassSwapCurveTests
{
    private const double Mix = 10;

    [Theory(DisplayName = "incoming_is_fully_cut_before_the_ramp")]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4.7)]
    public void Cut_Incoming_IsOne_BeforeRamp(double elapsed)
    {
        // Act
        double cut = BassSwapCurve.Cut(EBassSwapRole.Incoming, elapsed, Mix);

        // Assert
        Assert.Equal(1, cut);
    }

    [Theory(DisplayName = "outgoing_is_neutral_before_the_ramp")]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4.7)]
    public void Cut_Outgoing_IsZero_BeforeRamp(double elapsed)
    {
        // Act
        double cut = BassSwapCurve.Cut(EBassSwapRole.Outgoing, elapsed, Mix);

        // Assert
        Assert.Equal(0, cut);
    }

    [Theory(DisplayName = "both_roles_are_half_cut_at_the_middle_of_the_mix")]
    [InlineData(EBassSwapRole.Incoming)]
    [InlineData(EBassSwapRole.Outgoing)]
    public void Cut_AtMiddle_IsHalf(EBassSwapRole role)
    {
        // Act
        double cut = BassSwapCurve.Cut(role, Mix / 2, Mix);

        // Assert
        Assert.Equal(0.5, cut, 12);
    }

    [Fact(DisplayName = "ramp_lasts_half_a_second_around_the_middle")]
    public void Cut_RampBounds()
    {
        // Act
        double incomingStart = BassSwapCurve.Cut(EBassSwapRole.Incoming, 4.75, Mix);
        double incomingEnd = BassSwapCurve.Cut(EBassSwapRole.Incoming, 5.25, Mix);
        double outgoingStart = BassSwapCurve.Cut(EBassSwapRole.Outgoing, 4.75, Mix);
        double outgoingEnd = BassSwapCurve.Cut(EBassSwapRole.Outgoing, 5.25, Mix);

        // Assert
        Assert.Equal(1, incomingStart, 12);
        Assert.Equal(0, incomingEnd, 12);
        Assert.Equal(0, outgoingStart, 12);
        Assert.Equal(1, outgoingEnd, 12);
    }

    [Theory(DisplayName = "roles_are_complementary_during_the_ramp")]
    [InlineData(4.8)]
    [InlineData(5.1)]
    [InlineData(5.2)]
    public void Cut_Roles_AreComplementary(double elapsed)
    {
        // Act
        double incoming = BassSwapCurve.Cut(EBassSwapRole.Incoming, elapsed, Mix);
        double outgoing = BassSwapCurve.Cut(EBassSwapRole.Outgoing, elapsed, Mix);

        // Assert
        Assert.Equal(1, incoming + outgoing, 12);
    }

    [Theory(DisplayName = "incoming_is_neutral_and_outgoing_fully_cut_at_the_end_of_the_mix")]
    [InlineData(10)]
    [InlineData(12)]
    public void Cut_AtEnd(double elapsed)
    {
        // Act
        double incoming = BassSwapCurve.Cut(EBassSwapRole.Incoming, elapsed, Mix);
        double outgoing = BassSwapCurve.Cut(EBassSwapRole.Outgoing, elapsed, Mix);

        // Assert
        Assert.Equal(0, incoming);
        Assert.Equal(1, outgoing);
    }

    [Theory(DisplayName = "swap_applies_from_two_seconds")]
    [InlineData(1.99, false)]
    [InlineData(2, true)]
    public void Applies_Threshold(double mixSeconds, bool expected)
    {
        // Act
        bool applies = BassSwapCurve.Applies(mixSeconds);

        // Assert
        Assert.Equal(expected, applies);
    }

    [Theory(DisplayName = "cut_is_zero_on_a_mix_shorter_than_the_threshold")]
    [InlineData(EBassSwapRole.Incoming, 0)]
    [InlineData(EBassSwapRole.Incoming, 0.75)]
    [InlineData(EBassSwapRole.Outgoing, 0.75)]
    [InlineData(EBassSwapRole.Outgoing, 1.5)]
    public void Cut_ShortMix_IsZero(EBassSwapRole role, double elapsed)
    {
        // Act
        double cut = BassSwapCurve.Cut(role, elapsed, 1.5);

        // Assert
        Assert.Equal(0, cut);
    }

    [Theory(DisplayName = "elapsed_time_is_clamped_to_the_mix")]
    [InlineData(-5)]
    [InlineData(99)]
    public void Cut_OutOfRange_IsClamped(double elapsed)
    {
        // Act
        double incoming = BassSwapCurve.Cut(EBassSwapRole.Incoming, elapsed, Mix);
        double outgoing = BassSwapCurve.Cut(EBassSwapRole.Outgoing, elapsed, Mix);

        // Assert
        Assert.InRange(incoming, 0, 1);
        Assert.InRange(outgoing, 0, 1);
    }

    [Fact(DisplayName = "bass_swap_ramp_is_centred_on_the_requested_position")]
    public void Cut_WithSwapPosition_CentresTheRamp()
    {
        // Act & Assert
        Assert.Equal(1, BassSwapCurve.Cut(EBassSwapRole.Incoming, 2.7, Mix, 3), 6);
        Assert.Equal(0.5, BassSwapCurve.Cut(EBassSwapRole.Incoming, 3, Mix, 3), 6);
        Assert.Equal(0, BassSwapCurve.Cut(EBassSwapRole.Incoming, 3.3, Mix, 3), 6);
        Assert.Equal(0, BassSwapCurve.Cut(EBassSwapRole.Outgoing, 2.7, Mix, 3), 6);
        Assert.Equal(0.5, BassSwapCurve.Cut(EBassSwapRole.Outgoing, 3, Mix, 3), 6);
        Assert.Equal(1, BassSwapCurve.Cut(EBassSwapRole.Outgoing, 3.3, Mix, 3), 6);
    }

    [Theory(DisplayName = "bass_swap_defaults_to_the_middle")]
    [InlineData(EBassSwapRole.Incoming, 0)]
    [InlineData(EBassSwapRole.Incoming, 4.9)]
    [InlineData(EBassSwapRole.Incoming, 5.1)]
    [InlineData(EBassSwapRole.Outgoing, 5.2)]
    [InlineData(EBassSwapRole.Outgoing, 9)]
    public void Cut_WithoutSwapPosition_MatchesTheMiddle(EBassSwapRole role, double elapsed)
    {
        // Act
        double implicitMiddle = BassSwapCurve.Cut(role, elapsed, Mix);
        double explicitMiddle = BassSwapCurve.Cut(role, elapsed, Mix, Mix / 2);

        // Assert
        Assert.Equal(explicitMiddle, implicitMiddle);
    }

    [Fact(DisplayName = "resolve_swap_at_keeps_a_valid_position")]
    public void ResolveSwapAt_ValidPosition_IsKept()
    {
        // Act
        double swapAt = BassSwapCurve.ResolveSwapAt(2.5, 6);

        // Assert
        Assert.Equal(2.5, swapAt);
    }

    [Theory(DisplayName = "resolve_swap_at_falls_back_to_the_middle")]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(0.1)]
    [InlineData(5.9)]
    [InlineData(6.0)]
    [InlineData(7.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ResolveSwapAt_InvalidPosition_FallsBackToTheMiddle(double? requested)
    {
        // Act
        double swapAt = BassSwapCurve.ResolveSwapAt(requested, 6);

        // Assert
        Assert.Equal(3, swapAt);
    }
}