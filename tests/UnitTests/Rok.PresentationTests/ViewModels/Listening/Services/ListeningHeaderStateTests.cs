using Rok.Application.Dto;
using Rok.ViewModels.Listening.Services;

namespace Rok.PresentationTests.ViewModels.Listening.Services;

public class ListeningHeaderStateTests
{
    private static TrackDto FullTrack() => new()
    {
        Id = 1,
        ArtistId = 7,
        AlbumId = 3,
        GenreId = 2,
        GenreName = "Rock",
        CountryCode = "FR"
    };

    [Fact(DisplayName = "state_is_empty_without_track")]
    public void State_IsEmpty_WithoutTrack()
    {
        // Act
        var state = ListeningHeaderState.From(null, new ArtistDto { Id = 7 }, new AlbumDto { Id = 3 });

        // Assert
        Assert.True(state.IsEmpty);
        Assert.False(state.HasArtist);
        Assert.False(state.HasAlbum);
        Assert.False(state.ShowArtistAlbumSeparator);
        Assert.False(state.ShowArtistFavorite);
        Assert.False(state.ShowAlbumFavorite);
        Assert.False(state.HasYear);
        Assert.False(state.HasGenre);
        Assert.False(state.HasCountry);
        Assert.False(state.HasArtistLinks);
    }

    [Fact(DisplayName = "state_hides_album_and_separator_without_album")]
    public void State_HidesAlbumAndSeparator_WithoutAlbum()
    {
        // Arrange
        var track = FullTrack();
        track.AlbumId = null;

        // Act
        var state = ListeningHeaderState.From(track, new ArtistDto { Id = 7 }, null);

        // Assert
        Assert.False(state.HasAlbum);
        Assert.False(state.ShowAlbumFavorite);
        Assert.False(state.ShowArtistAlbumSeparator);
        Assert.True(state.HasArtist);
    }

    [Fact(DisplayName = "state_hides_artist_and_separator_without_artist")]
    public void State_HidesArtistAndSeparator_WithoutArtist()
    {
        // Arrange
        var track = FullTrack();
        track.ArtistId = null;

        // Act
        var state = ListeningHeaderState.From(track, null, new AlbumDto { Id = 3 });

        // Assert
        Assert.False(state.HasArtist);
        Assert.False(state.ShowArtistFavorite);
        Assert.False(state.ShowArtistAlbumSeparator);
        Assert.True(state.HasAlbum);
    }

    [Fact(DisplayName = "state_hides_year_genre_country_when_missing")]
    public void State_HidesYearGenreCountry_WhenMissing()
    {
        // Arrange
        var track = new TrackDto { Id = 1, ArtistId = 7, AlbumId = 3 };

        // Act
        var state = ListeningHeaderState.From(track, new ArtistDto { Id = 7 }, new AlbumDto { Id = 3 });

        // Assert
        Assert.False(state.HasYear);
        Assert.False(state.HasGenre);
        Assert.False(state.HasCountry);
    }

    [Fact(DisplayName = "state_shows_year_genre_country_when_present")]
    public void State_ShowsYearGenreCountry_WhenPresent()
    {
        // Act
        var state = ListeningHeaderState.From(FullTrack(), new ArtistDto { Id = 7 }, new AlbumDto { Id = 3, Year = 2001 });

        // Assert
        Assert.True(state.HasYear);
        Assert.True(state.HasGenre);
        Assert.True(state.HasCountry);
        Assert.True(state.ShowArtistFavorite);
        Assert.True(state.ShowAlbumFavorite);
        Assert.True(state.ShowArtistAlbumSeparator);
    }

    [Fact(DisplayName = "state_hides_favorite_when_loaded_entity_belongs_to_another_track")]
    public void State_HidesFavorite_WhenLoadedEntityBelongsToAnotherTrack()
    {
        // Act
        var state = ListeningHeaderState.From(FullTrack(), new ArtistDto { Id = 99 }, new AlbumDto { Id = 98 });

        // Assert
        Assert.False(state.ShowArtistFavorite);
        Assert.False(state.ShowAlbumFavorite);
    }

    [Theory(DisplayName = "state_has_artist_links_only_when_one_url_is_set")]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("https://www.youtube.com/@muse", true)]
    public void State_HasArtistLinks_OnlyWhenOneUrlIsSet(string? youtubeUrl, bool expected)
    {
        // Arrange
        var artist = new ArtistDto { Id = 7, YoutubeUrl = youtubeUrl };

        // Act
        var state = ListeningHeaderState.From(FullTrack(), artist, null);

        // Assert
        Assert.Equal(expected, state.HasArtistLinks);
    }
}