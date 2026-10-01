using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Filters;

namespace Rok.ApplicationTests;

public class FilterServiceTests
{
    private readonly TestFilterService _filterService;

    public FilterServiceTests()
    {
        Mock<IResourceService> mockResourceLoader = new();
        _filterService = new TestFilterService(mockResourceLoader.Object);
    }

    [Fact(DisplayName = "filter_by_genre_ids_keeps_single_genre_behaviour")]
    public void FilterByGenreIds_SingleGenre()
    {
        // Arrange
        List<TestAlbum> albums = new()
        {
            new() { GenreId = 1 },
            new() { GenreId = 2 },
            new() { GenreId = 1 }
        };

        // Act
        IEnumerable<TestAlbum> result = _filterService.FilterByGenreIds([1], albums);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, album => Assert.Equal(1, album.GenreId));
    }

    [Fact(DisplayName = "filter_by_genre_ids_returns_items_of_any_selected_genre")]
    public void FilterByGenreIds_AnySelectedGenre()
    {
        // Arrange
        List<TestAlbum> albums = new()
        {
            new() { GenreId = 1 },
            new() { GenreId = 2 },
            new() { GenreId = 3 }
        };

        // Act
        List<TestAlbum> result = _filterService.FilterByGenreIds([1, 2], albums).ToList();

        // Assert
        Assert.Equal([1L, 2L], result.Select(album => album.GenreId!.Value));
    }

    [Fact(DisplayName = "filter_by_genre_ids_returns_all_items_when_list_is_empty")]
    public void FilterByGenreIds_EmptyList()
    {
        // Arrange
        List<TestAlbum> albums = new()
        {
            new() { GenreId = 1 },
            new() { GenreId = null }
        };

        // Act
        IEnumerable<TestAlbum> result = _filterService.FilterByGenreIds([], albums);

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Theory(DisplayName = "filter_by_genre_ids_ignores_zero_ids")]
    [InlineData(new long[] { 0 }, 3)]
    [InlineData(new long[] { 0, 1 }, 1)]
    public void FilterByGenreIds_IgnoresZero(long[] genreIds, int expectedCount)
    {
        // Arrange
        List<TestAlbum> albums = new()
        {
            new() { GenreId = 1 },
            new() { GenreId = 2 },
            new() { GenreId = null }
        };

        // Act
        IEnumerable<TestAlbum> result = _filterService.FilterByGenreIds(genreIds, albums);

        // Assert
        Assert.Equal(expectedCount, result.Count());
    }

    [Fact(DisplayName = "filter_by_genre_ids_excludes_items_without_genre")]
    public void FilterByGenreIds_ExcludesItemsWithoutGenre()
    {
        // Arrange
        List<TestAlbum> albums = new()
        {
            new() { GenreId = 1 },
            new() { GenreId = null }
        };

        // Act
        IEnumerable<TestAlbum> result = _filterService.FilterByGenreIds([1], albums);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void FilterByTags_ShouldFilterCorrectly()
    {
        // Arrange
        List<TestAlbum> albums = new()
        {
            new() { Tags = new List<string> { "rock", "pop" } },
            new() { Tags = new List<string> { "jazz" } },
            new() { Tags = new List<string> { "rock", "jazz" } }
        };

        // Act
        IEnumerable<TestAlbum> result = _filterService.FilterByTags(new List<string> { "rock" }, albums);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, album => Assert.Contains("rock", album.Tags));
    }

    [Fact]
    public void Filter_ShouldUseRegisteredStrategy()
    {
        // Arrange
        List<TestAlbum> albums = new()
        {
            new() { IsGenreFavorite = true },
            new() { IsGenreFavorite = false }
        };

        // Act
        IEnumerable<TestAlbum> result = _filterService.Filter("GenreFavorite", albums);

        // Assert
        Assert.Single(result);
        Assert.All(result, album => Assert.True(album.IsGenreFavorite));
    }


    public class TestFilterService(IResourceService resourceLoader) : FilterService<TestAlbum>(resourceLoader)
    {
        protected override void RegisterFilterStrategies()
        {
            RegisterFilter("GenreFavorite", albums => albums.Where(a => a.IsGenreFavorite));
            RegisterFilter("NeverListened", albums => albums.Where(a => a.ListenCount == 0));
        }

        public override string GetLabel(string filterBy) => filterBy switch
        {
            "GenreFavorite" => "Favorite Genre",
            "NeverListened" => "Never Listened",
            _ => "Unknown"
        };
    }

    public class TestAlbum : IFilterable
    {
        public long? GenreId { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool IsGenreFavorite { get; set; }
        public int ListenCount { get; set; }
    }
}