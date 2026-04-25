using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Filters;

namespace Rok.ApplicationTests.Services.Filters;

public class TracksFilterTests
{
    private static TracksFilter CreateFilter(IResourceService? resource = null) => new(resource ?? Mock.Of<IResourceService>());

    private sealed class FakeTrack : IFilterableTrack
    {
        public bool IsArtistFavorite { get; set; }
        public bool IsAlbumFavorite { get; set; }
        public bool IsGenreFavorite { get; set; }
        public int Score { get; set; }
        public bool IsLive { get; set; }
        public int ListenCount { get; set; }
        public long? GenreId { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    [Fact(DisplayName = "TracksFilter Filter by artist favorite should keep only tracks whose artist is favorite")]
    public void Filter_ByArtistFavorite_ShouldKeepOnlyArtistFavoriteTracks()
    {
        // Arrange
        TracksFilter sut = CreateFilter();
        List<IFilterableTrack> input = new() { new FakeTrack { IsArtistFavorite = true }, new FakeTrack { IsArtistFavorite = false } };

        // Act
        List<IFilterableTrack> result = sut.Filter(TracksFilter.KFilterByArtistFavorite, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsArtistFavorite);
    }

    [Fact(DisplayName = "TracksFilter Filter by genre favorite should keep only tracks whose genre is favorite")]
    public void Filter_ByGenreFavorite_ShouldKeepOnlyGenreFavoriteTracks()
    {
        // Arrange
        TracksFilter sut = CreateFilter();
        List<IFilterableTrack> input = new() { new FakeTrack { IsGenreFavorite = true }, new FakeTrack { IsGenreFavorite = false } };

        // Act
        List<IFilterableTrack> result = sut.Filter(TracksFilter.KFilterByGenreFavorite, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsGenreFavorite);
    }

    [Fact(DisplayName = "TracksFilter Filter by album favorite should keep only tracks whose album is favorite")]
    public void Filter_ByAlbumFavorite_ShouldKeepOnlyAlbumFavoriteTracks()
    {
        // Arrange
        TracksFilter sut = CreateFilter();
        List<IFilterableTrack> input = new() { new FakeTrack { IsAlbumFavorite = true }, new FakeTrack { IsAlbumFavorite = false } };

        // Act
        List<IFilterableTrack> result = sut.Filter(TracksFilter.KFilterByAlbumFavorite, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsAlbumFavorite);
    }

    [Fact(DisplayName = "TracksFilter Filter by track favorite should keep only tracks with positive score")]
    public void Filter_ByTrackFavorite_ShouldKeepOnlyTracksWithPositiveScore()
    {
        // Arrange
        TracksFilter sut = CreateFilter();
        List<IFilterableTrack> input = new() { new FakeTrack { Score = 1 }, new FakeTrack { Score = 0 }, new FakeTrack { Score = -3 } };

        // Act
        List<IFilterableTrack> result = sut.Filter(TracksFilter.KFilterByTrackFavorite, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].Score);
    }

    [Fact(DisplayName = "TracksFilter Filter by never listened should keep only tracks with zero listen count")]
    public void Filter_ByNeverListened_ShouldKeepOnlyTracksWithZeroListenCount()
    {
        // Arrange
        TracksFilter sut = CreateFilter();
        List<IFilterableTrack> input = new() { new FakeTrack { ListenCount = 0 }, new FakeTrack { ListenCount = 4 } };

        // Act
        List<IFilterableTrack> result = sut.Filter(TracksFilter.KFilterByNeverListened, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(0, result[0].ListenCount);
    }

    [Fact(DisplayName = "TracksFilter Filter by live should keep only live tracks")]
    public void Filter_ByLive_ShouldKeepOnlyLiveTracks()
    {
        // Arrange
        TracksFilter sut = CreateFilter();
        List<IFilterableTrack> input = new() { new FakeTrack { IsLive = true }, new FakeTrack { IsLive = false } };

        // Act
        List<IFilterableTrack> result = sut.Filter(TracksFilter.KFilterByLive, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsLive);
    }

    [Fact(DisplayName = "TracksFilter Filter with unknown key should return all items unchanged")]
    public void Filter_WithUnknownKey_ShouldReturnAllItemsUnchanged()
    {
        // Arrange
        TracksFilter sut = CreateFilter();
        List<IFilterableTrack> input = new() { new FakeTrack(), new FakeTrack { IsLive = true } };

        // Act
        List<IFilterableTrack> result = sut.Filter("UNKNOWN_KEY", input).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Theory(DisplayName = "TracksFilter GetLabel should request the expected resource key for each filter")]
    [InlineData(TracksFilter.KFilterByArtistFavorite, "tracksViewFilterByFavoriteArtist")]
    [InlineData(TracksFilter.KFilterByGenreFavorite, "tracksViewFilterByFavoriteGenre")]
    [InlineData(TracksFilter.KFilterByAlbumFavorite, "tracksViewFilterByFavoriteAlbum")]
    [InlineData(TracksFilter.KFilterByTrackFavorite, "tracksViewFilterByFavoriteTrack")]
    [InlineData(TracksFilter.KFilterByNeverListened, "tracksViewFilterByNeverListened")]
    [InlineData(TracksFilter.KFilterByLive, "tracksViewFilterByLive")]
    [InlineData("UNKNOWN", "tracksViewFilterNone")]
    public void GetLabel_ShouldRequestExpectedResourceKey(string filterBy, string expectedResourceKey)
    {
        // Arrange
        Mock<IResourceService> resource = new();
        resource.Setup(r => r.GetString(expectedResourceKey)).Returns("LABEL");
        TracksFilter sut = CreateFilter(resource.Object);

        // Act
        string label = sut.GetLabel(filterBy);

        // Assert
        Assert.Equal("LABEL", label);
        resource.Verify(r => r.GetString(expectedResourceKey), Times.Once);
    }
}
