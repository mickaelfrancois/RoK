using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Filters;

namespace Rok.ApplicationTests.Services.Filters;

public class ArtistsFilterTests
{
    private static ArtistsFilter CreateFilter(IResourceService? resource = null) => new(resource ?? Mock.Of<IResourceService>());

    private sealed class FakeArtist : IFilterableArtist
    {
        public bool IsFavorite { get; set; }
        public bool IsGenreFavorite { get; set; }
        public int ListenCount { get; set; }
        public long? GenreId { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    [Fact(DisplayName = "ArtistsFilter Filter by favorite artist should keep only favorite artists")]
    public void Filter_ByFavoriteArtist_ShouldKeepOnlyFavorites()
    {
        // Arrange
        ArtistsFilter sut = CreateFilter();
        List<IFilterableArtist> input = new() { new FakeArtist { IsFavorite = true }, new FakeArtist { IsFavorite = false } };

        // Act
        List<IFilterableArtist> result = sut.Filter(ArtistsFilter.KFilterByFavoriteArtist, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsFavorite);
    }

    [Fact(DisplayName = "ArtistsFilter Filter by genre favorite should keep only artists whose genre is favorite")]
    public void Filter_ByGenreFavorite_ShouldKeepOnlyGenreFavoriteArtists()
    {
        // Arrange
        ArtistsFilter sut = CreateFilter();
        List<IFilterableArtist> input = new() { new FakeArtist { IsGenreFavorite = true }, new FakeArtist { IsGenreFavorite = false } };

        // Act
        List<IFilterableArtist> result = sut.Filter(ArtistsFilter.KFilterByGenreFavorite, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsGenreFavorite);
    }

    [Fact(DisplayName = "ArtistsFilter Filter by never listened should keep only artists with zero listen count")]
    public void Filter_ByNeverListened_ShouldKeepOnlyArtistsWithZeroListenCount()
    {
        // Arrange
        ArtistsFilter sut = CreateFilter();
        List<IFilterableArtist> input = new() { new FakeArtist { ListenCount = 0 }, new FakeArtist { ListenCount = 12 } };

        // Act
        List<IFilterableArtist> result = sut.Filter(ArtistsFilter.KFilterByNeverListened, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(0, result[0].ListenCount);
    }

    [Fact(DisplayName = "ArtistsFilter Filter with unknown key should return all items unchanged")]
    public void Filter_WithUnknownKey_ShouldReturnAllItemsUnchanged()
    {
        // Arrange
        ArtistsFilter sut = CreateFilter();
        List<IFilterableArtist> input = new() { new FakeArtist(), new FakeArtist { IsFavorite = true } };

        // Act
        List<IFilterableArtist> result = sut.Filter("UNKNOWN_KEY", input).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Theory(DisplayName = "ArtistsFilter GetLabel should request the expected resource key for each filter")]
    [InlineData(ArtistsFilter.KFilterByFavoriteArtist, "artistsViewFilterByFavoriteArtist")]
    [InlineData(ArtistsFilter.KFilterByGenreFavorite, "artistsViewFilterByFavoriteGenre")]
    [InlineData(ArtistsFilter.KFilterByNeverListened, "artistsViewFilterByNeverListened")]
    [InlineData("UNKNOWN", "artistsViewFilterNone")]
    public void GetLabel_ShouldRequestExpectedResourceKey(string filterBy, string expectedResourceKey)
    {
        // Arrange
        Mock<IResourceService> resource = new();
        resource.Setup(r => r.GetString(expectedResourceKey)).Returns("LABEL");
        ArtistsFilter sut = CreateFilter(resource.Object);

        // Act
        string label = sut.GetLabel(filterBy);

        // Assert
        Assert.Equal("LABEL", label);
        resource.Verify(r => r.GetString(expectedResourceKey), Times.Once);
    }
}
