using Microsoft.UI.Xaml;
using Rok.Commons;

namespace Rok.PresentationTests.Commons;

public class RatingControlDarkControlTests
{
    [Fact(DisplayName = "IsClearEnabled should default to true so a rating can be removed by clicking it again")]
    public void IsClearEnabled_ShouldDefaultToTrue()
    {
        // Act
        PropertyMetadata metadata = RatingControlDarkControl.IsClearEnabledProperty.GetMetadata(typeof(bool));

        // Assert
        Assert.True((bool)metadata.DefaultValue);
    }

    [Theory(DisplayName = "IsSetByUser should tell a user change from a binding change")]
    [InlineData(4.0, 2, 4, true)]
    [InlineData(2.0, 2, 4, false)]
    [InlineData(-1.0, 0, -1, false)]
    [InlineData(-1.0, 3, -1, true)]
    [InlineData(0.0, 3, 0, true)]
    [InlineData(-1.0, -1, 0, false)]
    public void IsSetByUser_ShouldClassifyTheChange(double innerValue, int previousScore, int newScore, bool expected)
    {
        // Act
        bool result = RatingControlDarkControl.IsSetByUser(innerValue, previousScore, newScore);

        // Assert
        Assert.Equal(expected, result);
    }
}