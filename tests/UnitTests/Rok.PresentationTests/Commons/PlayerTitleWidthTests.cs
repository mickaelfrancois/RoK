using Rok.Commons;

namespace Rok.PresentationTests.Commons;

public class PlayerTitleWidthTests
{
    [Fact(DisplayName = "compute_is_bounded_by_column_right_when_buttons_are_far")]
    public void Compute_IsBoundedByColumnRight_WhenButtonsAreFar()
    {
        // Act
        double width = PlayerTitleWidth.Compute(80, 900, 400, 0, 12, 100);

        // Assert
        Assert.Equal(320, width);
    }

    [Fact(DisplayName = "compute_is_bounded_by_buttons_when_they_are_close")]
    public void Compute_IsBoundedByButtons_WhenTheyAreClose()
    {
        // Act
        double width = PlayerTitleWidth.Compute(80, 281, 430, 0, 12, 100);

        // Assert
        Assert.Equal(189, width);
    }

    [Fact(DisplayName = "compute_subtracts_the_score_block_when_visible")]
    public void Compute_SubtractsScoreBlock_WhenVisible()
    {
        // Act
        double without = PlayerTitleWidth.Compute(80, 900, 580, 0, 12, 100);
        double with = PlayerTitleWidth.Compute(80, 900, 580, 180, 12, 100);

        // Assert
        Assert.Equal(180, without - with);
    }

    [Theory(DisplayName = "compute_returns_min_width_when_no_room_or_invalid_input")]
    [InlineData(80, 120, 300, 0, 12, 100)]
    [InlineData(80, 281, 430, 400, 12, 100)]
    [InlineData(double.NaN, 281, 430, 0, 12, 100)]
    [InlineData(80, double.PositiveInfinity, 430, 0, 12, 100)]
    [InlineData(80, 281, double.NaN, 0, 12, 100)]
    [InlineData(80, 281, 430, double.NegativeInfinity, 12, 100)]
    public void Compute_ReturnsMinWidth_WhenNoRoomOrInvalidInput(
        double titleLeft, double controlsLeft, double columnRight, double scoreBlock, double gap, double minWidth)
    {
        // Act
        double width = PlayerTitleWidth.Compute(titleLeft, controlsLeft, columnRight, scoreBlock, gap, minWidth);

        // Assert
        Assert.Equal(minWidth, width);
    }

    [Fact(DisplayName = "compute_exceeds_the_old_fixed_width_in_medium_layout_at_1199px")]
    public void Compute_ExceedsOldFixedWidth_InMediumLayoutAt1199Px()
    {
        // Act
        double width = PlayerTitleWidth.Compute(80, 480, 354, 0, 12, 100);

        // Assert
        Assert.Equal(274, width);
        Assert.True(width > 200);
    }
}