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
}