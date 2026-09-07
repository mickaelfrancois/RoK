using Rok.Commons;

namespace Rok.PresentationTests.Commons;

public class ScoreAnimationPlanTests
{
    [Theory(DisplayName = "For should return no plan when the score does not actually change")]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, 0)]
    public void For_ShouldReturnNoPlan_WhenScoreIsUnchanged(int previousScore, int newScore)
    {
        // Act
        ScoreAnimationPlan? plan = ScoreAnimationPlan.For(previousScore, newScore);

        // Assert
        Assert.Null(plan);
    }

    [Theory(DisplayName = "For should shrink the row when the score is removed")]
    [InlineData(5, 0)]
    [InlineData(2, -1)]
    public void For_ShouldShrinkTheRow_WhenScoreIsRemoved(int previousScore, int newScore)
    {
        // Act
        ScoreAnimationPlan? plan = ScoreAnimationPlan.For(previousScore, newScore);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.ScaleTo < 1);
        Assert.False(plan.FlashSelectedColor);
        Assert.Equal(0, plan.WobbleDegrees);
    }

    [Theory(DisplayName = "For should keep the low tier sober, without flash nor wobble")]
    [InlineData(1)]
    [InlineData(2)]
    public void For_ShouldKeepLowTierSober(int newScore)
    {
        // Act
        ScoreAnimationPlan? plan = ScoreAnimationPlan.For(0, newScore);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.ScaleTo > 1);
        Assert.False(plan.FlashSelectedColor);
        Assert.Equal(0, plan.WobbleDegrees);
    }

    [Fact(DisplayName = "For should flash the middle tier but keep it steady")]
    public void For_ShouldFlashMiddleTier_WithoutWobble()
    {
        // Act
        ScoreAnimationPlan? plan = ScoreAnimationPlan.For(0, 3);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.FlashSelectedColor);
        Assert.Equal(0, plan.WobbleDegrees);
    }

    [Theory(DisplayName = "For should flash and wobble the top tier")]
    [InlineData(4)]
    [InlineData(5)]
    public void For_ShouldFlashAndWobbleTopTier(int newScore)
    {
        // Act
        ScoreAnimationPlan? plan = ScoreAnimationPlan.For(0, newScore);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.FlashSelectedColor);
        Assert.True(plan.WobbleDegrees > 0);
    }

    [Fact(DisplayName = "For should grow the gesture as the score gets higher")]
    public void For_ShouldGrowGesture_WithHigherScore()
    {
        // Act
        ScoreAnimationPlan? low = ScoreAnimationPlan.For(0, 2);
        ScoreAnimationPlan? middle = ScoreAnimationPlan.For(0, 3);
        ScoreAnimationPlan? top = ScoreAnimationPlan.For(0, 5);

        // Assert
        Assert.NotNull(low);
        Assert.NotNull(middle);
        Assert.NotNull(top);
        Assert.True(low.ScaleTo < middle.ScaleTo);
        Assert.True(middle.ScaleTo < top.ScaleTo);
        Assert.True(low.Duration < middle.Duration);
        Assert.True(middle.Duration < top.Duration);
    }
}