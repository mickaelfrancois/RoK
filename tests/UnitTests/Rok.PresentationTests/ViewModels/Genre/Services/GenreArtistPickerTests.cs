using Rok.ViewModels.Genre.Services;

namespace Rok.PresentationTests.ViewModels.Genre.Services;

public class GenreArtistPickerTests
{
    [Fact(DisplayName = "when_there_is_no_album_then_no_artist_is_picked")]
    public void PickArtistName_ShouldReturnNull_WhenThereIsNoAlbum()
    {
        // Arrange
        List<string?> names = [];

        // Act
        string? result = GenreArtistPicker.PickArtistName(names, new Random(1));

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "when_no_album_carries_an_artist_name_then_no_artist_is_picked")]
    public void PickArtistName_ShouldReturnNull_WhenNoAlbumCarriesAnArtistName()
    {
        // Arrange
        List<string?> names = ["", null, ""];

        // Act
        string? result = GenreArtistPicker.PickArtistName(names, new Random(1));

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "when_some_albums_carry_an_artist_name_then_one_of_them_is_picked")]
    public void PickArtistName_ShouldPickOneOfTheKnownNames()
    {
        // Arrange
        List<string?> names = ["", "Artist A", null, "Artist B"];

        // Act
        List<string?> picks = Enumerable.Range(0, 50).Select(seed => GenreArtistPicker.PickArtistName(names, new Random(seed))).ToList();

        // Assert
        Assert.All(picks, pick => Assert.Contains(pick, new[] { "Artist A", "Artist B" }));
        Assert.Contains("Artist A", picks);
        Assert.Contains("Artist B", picks);
    }
}