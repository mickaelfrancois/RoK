using Rok.ViewModels.Genre.Services;

namespace Rok.PresentationTests.ViewModels.Genre.Services;

public class GenreArtistPickerTests
{
    [Fact(DisplayName = "when_there_is_no_album_then_no_artist_is_picked")]
    public void PickMostListenedArtist_ShouldReturnNull_WhenThereIsNoAlbum()
    {
        // Arrange
        List<(string? ArtistName, int ListenCount)> albums = [];

        // Act
        string? result = GenreArtistPicker.PickMostListenedArtist(albums);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "when_no_album_carries_an_artist_name_then_no_artist_is_picked")]
    public void PickMostListenedArtist_ShouldReturnNull_WhenNoAlbumCarriesAnArtistName()
    {
        // Arrange
        List<(string? ArtistName, int ListenCount)> albums = [("", 5), (null, 9)];

        // Act
        string? result = GenreArtistPicker.PickMostListenedArtist(albums);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "when_albums_have_listens_then_the_artist_with_the_most_listens_across_albums_is_picked")]
    public void PickMostListenedArtist_ShouldSumTheListensPerArtist()
    {
        // Arrange
        List<(string? ArtistName, int ListenCount)> albums = [("Artist A", 10), ("Artist B", 12), ("Artist A", 5), ("Artist B", 1)];

        // Act
        string? result = GenreArtistPicker.PickMostListenedArtist(albums);

        // Assert
        Assert.Equal("Artist A", result);
    }

    [Fact(DisplayName = "when_listens_are_tied_then_the_artist_with_the_most_albums_is_picked")]
    public void PickMostListenedArtist_ShouldPreferTheArtistWithMoreAlbums_WhenListensAreTied()
    {
        // Arrange
        List<(string? ArtistName, int ListenCount)> albums = [("Artist A", 4), ("Artist B", 2), ("Artist B", 2)];

        // Act
        string? result = GenreArtistPicker.PickMostListenedArtist(albums);

        // Assert
        Assert.Equal("Artist B", result);
    }

    [Fact(DisplayName = "when_nothing_was_listened_then_the_first_artist_by_name_is_picked")]
    public void PickMostListenedArtist_ShouldFallBackToTheName_WhenNothingWasListened()
    {
        // Arrange
        List<(string? ArtistName, int ListenCount)> albums = [("Zed", 0), ("alpha", 0)];

        // Act
        string? result = GenreArtistPicker.PickMostListenedArtist(albums);

        // Assert
        Assert.Equal("alpha", result);
    }

    [Fact(DisplayName = "when_ranking_artists_then_they_follow_listens_albums_then_name")]
    public void RankByListens_ShouldOrderByListensThenAlbumsThenName()
    {
        // Arrange
        List<(string? ArtistName, int ListenCount)> albums = [("C", 1), ("B", 5), ("A", 5), ("D", 9), ("", 99), ("C", 1)];

        // Act
        IReadOnlyList<string> result = GenreArtistPicker.RankByListens(albums);

        // Assert
        Assert.Equal(["D", "A", "B", "C"], result);
    }
}