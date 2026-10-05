using Rok.Services.Taskbar;

namespace Rok.PresentationTests.Services.Taskbar;

public class ThumbBarIconSetTests
{
    [Fact(DisplayName = "disposing_the_icon_set_destroys_every_icon_once")]
    public void Dispose_DestroysEveryHandleOnce()
    {
        // Arrange
        var destroyed = new List<nint>();
        var set = new ThumbBarIconSet(1, 2, 3, 4, destroyed.Add);

        // Act
#pragma warning disable IDISP017
        set.Dispose();
        set.Dispose();
#pragma warning restore IDISP017

        // Assert
        Assert.Equal(4, destroyed.Count);
        Assert.Equal([1, 2, 3, 4], destroyed.Order());
    }

    [Fact(DisplayName = "disposing_the_icon_set_skips_empty_handles")]
    public void Dispose_SkipsZeroHandles()
    {
        // Arrange
        var destroyed = new List<nint>();
        var set = new ThumbBarIconSet(0, 2, 0, 4, destroyed.Add);

        // Act
#pragma warning disable IDISP017
        set.Dispose();
#pragma warning restore IDISP017

        // Assert
        Assert.Equal([2, 4], destroyed.Order());
    }

    [Fact(DisplayName = "icon_set_exposes_the_handles_it_was_built_with")]
    public void Properties_ExposeHandles()
    {
        // Arrange
        // Act
        using var set = new ThumbBarIconSet(1, 2, 3, 4, _ => { });

        // Assert
        Assert.Equal(1, set.Previous);
        Assert.Equal(2, set.Play);
        Assert.Equal(3, set.Pause);
        Assert.Equal(4, set.Next);
    }
}