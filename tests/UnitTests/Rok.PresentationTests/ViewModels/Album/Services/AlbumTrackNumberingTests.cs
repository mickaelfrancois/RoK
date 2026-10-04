using Rok.ViewModels.Album.Services;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumTrackNumberingTests
{
    [Fact(DisplayName = "when_every_track_has_a_unique_positive_number_then_the_numbers_can_be_used")]
    public void CanUseTrackNumbers_ShouldBeTrue_WhenNumbersAreUnique()
    {
        // Arrange
        List<int?> numbers = [1, 2, 3, 4];

        // Act
        bool result = AlbumTrackNumbering.CanUseTrackNumbers(numbers);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "when_the_numbers_have_gaps_then_they_can_still_be_used")]
    public void CanUseTrackNumbers_ShouldBeTrue_WhenNumbersHaveGaps()
    {
        // Arrange
        List<int?> numbers = [2, 3, 7];

        // Act
        bool result = AlbumTrackNumbering.CanUseTrackNumbers(numbers);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "when_a_number_repeats_then_the_numbers_cannot_be_used")]
    public void CanUseTrackNumbers_ShouldBeFalse_WhenANumberRepeats()
    {
        // Arrange
        List<int?> numbers = [1, 2, 1, 2];

        // Act
        bool result = AlbumTrackNumbering.CanUseTrackNumbers(numbers);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "when_a_track_has_no_usable_number_then_the_numbers_cannot_be_used")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void CanUseTrackNumbers_ShouldBeFalse_WhenATrackHasNoUsableNumber(int? missing)
    {
        // Arrange
        List<int?> numbers = [1, missing, 3];

        // Act
        bool result = AlbumTrackNumbering.CanUseTrackNumbers(numbers);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "when_there_is_no_track_then_the_numbers_cannot_be_used")]
    public void CanUseTrackNumbers_ShouldBeFalse_WhenThereIsNoTrack()
    {
        // Arrange
        List<int?> numbers = [];

        // Act
        bool result = AlbumTrackNumbering.CanUseTrackNumbers(numbers);

        // Assert
        Assert.False(result);
    }
}