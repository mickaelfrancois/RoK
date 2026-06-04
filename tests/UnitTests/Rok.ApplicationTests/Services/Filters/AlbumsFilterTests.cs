using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Filters;

namespace Rok.ApplicationTests.Services.Filters;

public class AlbumsFilterTests
{
    private static AlbumsFilter CreateFilter(IResourceService? resource = null, TimeProvider? timeProvider = null)
        => new(resource ?? Mock.Of<IResourceService>(), timeProvider ?? new FakeTimeProvider());

    private sealed class FakeAlbum : IFilterableAlbum
    {
        public bool IsFavorite { get; set; }
        public bool IsArtistFavorite { get; set; }
        public bool IsAlbumFavorite { get; set; }
        public bool IsGenreFavorite { get; set; }
        public bool IsLive { get; set; }
        public bool IsBestOf { get; set; }
        public bool IsCompilation { get; set; }
        public int ListenCount { get; set; }
        public long? GenreId { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    [Fact(DisplayName = "AlbumsFilter Filter by album favorite should keep only favorite albums")]
    public void Filter_ByAlbumFavorite_ShouldKeepOnlyFavorites()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new() { new FakeAlbum { IsFavorite = true }, new FakeAlbum { IsFavorite = false } };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByAlbumFavorite, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsFavorite);
    }

    [Fact(DisplayName = "AlbumsFilter Filter by artist favorite should keep only albums whose artist is favorite")]
    public void Filter_ByArtistFavorite_ShouldKeepOnlyArtistFavoriteAlbums()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new() { new FakeAlbum { IsArtistFavorite = true }, new FakeAlbum { IsArtistFavorite = false } };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByArtistFavorite, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsArtistFavorite);
    }

    [Fact(DisplayName = "AlbumsFilter Filter by genre favorite should keep only albums whose genre is favorite")]
    public void Filter_ByGenreFavorite_ShouldKeepOnlyGenreFavoriteAlbums()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new() { new FakeAlbum { IsGenreFavorite = true }, new FakeAlbum { IsGenreFavorite = false } };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByGenreFavorite, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsGenreFavorite);
    }

    [Fact(DisplayName = "AlbumsFilter Filter by never listened should keep only albums with zero listen count")]
    public void Filter_ByNeverListened_ShouldKeepOnlyAlbumsWithZeroListenCount()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new() { new FakeAlbum { ListenCount = 0 }, new FakeAlbum { ListenCount = 5 } };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByNeverListened, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(0, result[0].ListenCount);
    }

    [Fact(DisplayName = "AlbumsFilter Filter by live should keep only live albums")]
    public void Filter_ByLive_ShouldKeepOnlyLiveAlbums()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new() { new FakeAlbum { IsLive = true }, new FakeAlbum { IsLive = false } };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByLive, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsLive);
    }

    [Fact(DisplayName = "AlbumsFilter Filter by best of should keep only best of albums")]
    public void Filter_ByBestOf_ShouldKeepOnlyBestOfAlbums()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new() { new FakeAlbum { IsBestOf = true }, new FakeAlbum { IsBestOf = false } };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByBestOf, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsBestOf);
    }

    [Fact(DisplayName = "AlbumsFilter Filter by compilation should keep only compilation albums")]
    public void Filter_ByCompilation_ShouldKeepOnlyCompilationAlbums()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new() { new FakeAlbum { IsCompilation = true }, new FakeAlbum { IsCompilation = false } };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByCompilation, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsCompilation);
    }

    [Fact(DisplayName = "AlbumsFilter Filter by album should exclude live compilation and best of")]
    public void Filter_ByAlbum_ShouldExcludeLiveCompilationAndBestOf()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new()
        {
            new FakeAlbum(),
            new FakeAlbum { IsLive = true },
            new FakeAlbum { IsCompilation = true },
            new FakeAlbum { IsBestOf = true }
        };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByAlbum, input).ToList();

        // Assert
        Assert.Single(result);
    }

    [Theory(DisplayName = "AlbumsFilter IsAnniversary should match anniversaries within the window and ignore recent or future releases")]
    [InlineData(2020, 6, 15, true)]   // exact anniversary day
    [InlineData(2020, 6, 18, true)]   // window upper edge (+3 days)
    [InlineData(2020, 6, 12, true)]   // window lower edge (-3 days)
    [InlineData(2020, 6, 19, false)]  // one day outside the window
    [InlineData(2020, 6, 11, false)]  // one day outside the window
    [InlineData(2025, 6, 16, true)]   // first anniversary falls tomorrow, inside the window
    [InlineData(2026, 6, 15, false)]  // released today, no anniversary yet
    [InlineData(2027, 1, 1, false)]   // future release date
    public void IsAnniversary_ShouldMatchWindowAndIgnoreRecentOrFutureReleases(int year, int month, int day, bool expected)
    {
        // Arrange
        DateTime releaseDate = new(year, month, day);
        DateOnly today = new(2026, 6, 15);

        // Act
        bool result = AlbumsFilter.IsAnniversary(releaseDate, today);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "AlbumsFilter IsAnniversary with no release date should return false")]
    public void IsAnniversary_WithNoReleaseDate_ShouldReturnFalse()
    {
        // Arrange
        DateOnly today = new(2026, 6, 15);

        // Act
        bool result = AlbumsFilter.IsAnniversary(null, today);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "AlbumsFilter IsAnniversary with a leap day release should match february 28 on non leap years")]
    public void IsAnniversary_WithLeapDayRelease_ShouldMatchFebruary28OnNonLeapYears()
    {
        // Arrange
        DateTime releaseDate = new(2020, 2, 29);
        DateOnly today = new(2026, 2, 28);

        // Act
        bool result = AlbumsFilter.IsAnniversary(releaseDate, today);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "AlbumsFilter IsAnniversary should match across a year boundary")]
    public void IsAnniversary_ShouldMatchAcrossYearBoundary()
    {
        // Arrange
        DateTime releaseDate = new(2020, 12, 31);
        DateOnly today = new(2026, 1, 2);

        // Act
        bool result = AlbumsFilter.IsAnniversary(releaseDate, today);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "AlbumsFilter Filter by anniversary should keep only albums celebrating an anniversary")]
    public void Filter_ByAnniversary_ShouldKeepOnlyAnniversaryAlbums()
    {
        // Arrange
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        AlbumsFilter sut = CreateFilter(timeProvider: timeProvider);
        List<IFilterableAlbum> input = new()
        {
            new FakeAlbum { ReleaseDate = new DateTime(2016, 6, 15) },
            new FakeAlbum { ReleaseDate = new DateTime(2016, 9, 1) },
            new FakeAlbum { ReleaseDate = null }
        };

        // Act
        List<IFilterableAlbum> result = sut.Filter(AlbumsFilter.KFilterByAnniversary, input).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(new DateTime(2016, 6, 15), result[0].ReleaseDate);
    }

    [Fact(DisplayName = "AlbumsFilter Filter with unknown key should return all items unchanged")]
    public void Filter_WithUnknownKey_ShouldReturnAllItemsUnchanged()
    {
        // Arrange
        AlbumsFilter sut = CreateFilter();
        List<IFilterableAlbum> input = new() { new FakeAlbum(), new FakeAlbum { IsLive = true } };

        // Act
        List<IFilterableAlbum> result = sut.Filter("UNKNOWN_KEY", input).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Theory(DisplayName = "AlbumsFilter GetLabel should request the expected resource key for each filter")]
    [InlineData(AlbumsFilter.KFilterByAlbumFavorite, "albumsViewFilterByFavoriteAlbum")]
    [InlineData(AlbumsFilter.KFilterByArtistFavorite, "albumsViewFilterByFavoriteArtist")]
    [InlineData(AlbumsFilter.KFilterByGenreFavorite, "albumsViewFilterByFavoriteGenre")]
    [InlineData(AlbumsFilter.KFilterByNeverListened, "albumsViewFilterByNeverListened")]
    [InlineData(AlbumsFilter.KFilterByLive, "albumsViewFilterByLive")]
    [InlineData(AlbumsFilter.KFilterByBestOf, "albumsViewFilterByBestof")]
    [InlineData(AlbumsFilter.KFilterByCompilation, "albumsViewFilterByCompilation")]
    [InlineData(AlbumsFilter.KFilterByAlbum, "albumsViewFilterByAlbum")]
    [InlineData(AlbumsFilter.KFilterByAnniversary, "albumsViewFilterByAnniversary")]
    [InlineData("UNKNOWN", "albumsViewFilterNone")]
    public void GetLabel_ShouldRequestExpectedResourceKey(string filterBy, string expectedResourceKey)
    {
        // Arrange
        Mock<IResourceService> resource = new();
        resource.Setup(r => r.GetString(expectedResourceKey)).Returns("LABEL");
        AlbumsFilter sut = CreateFilter(resource.Object);

        // Act
        string label = sut.GetLabel(filterBy);

        // Assert
        Assert.Equal("LABEL", label);
        resource.Verify(r => r.GetString(expectedResourceKey), Times.Once);
    }
}