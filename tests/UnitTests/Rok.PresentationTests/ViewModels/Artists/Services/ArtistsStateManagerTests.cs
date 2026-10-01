using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Grouping;
using Rok.ViewModels.Artists.Services;

namespace Rok.PresentationTests.ViewModels.Artists.Services;

public class ArtistsStateManagerTests
{
    private static Mock<IAppOptions> CreateOptionsMock(string groupBy = "", List<string>? filters = null, List<long>? genres = null, List<string>? tags = null)
    {
        Mock<IAppOptions> options = new();
        options.SetupProperty(o => o.ArtistsGroupBy, groupBy);
        options.SetupProperty(o => o.ArtistsFilterBy, filters ?? new List<string>());
        options.SetupProperty(o => o.ArtistsFilterByGenresId, genres ?? new List<long>());
        options.SetupProperty(o => o.ArtistsFilterByTags, tags ?? new List<string>());
        return options;
    }

    [Fact(DisplayName = "Load should fall back to Artist GroupBy when no value is stored")]
    public void Load_ShouldUseDefaultGroupBy_WhenStoredIsEmpty()
    {
        // Arrange
        ArtistsStateManager sut = new(CreateOptionsMock().Object);

        // Act
        sut.Load();

        // Assert
        Assert.Equal(GroupingConstants.Artist, sut.GroupBy);
    }

    [Fact(DisplayName = "Load should use the stored GroupBy and filters when present")]
    public void Load_ShouldUseStoredValues_WhenPresent()
    {
        // Arrange
        ArtistsStateManager sut = new(CreateOptionsMock(
            groupBy: GroupingConstants.Decade,
            filters: new() { "fav" },
            genres: new() { 5 },
            tags: new() { "jazz" }).Object);

        // Act
        sut.Load();

        // Assert
        Assert.Equal(GroupingConstants.Decade, sut.GroupBy);
        Assert.Single(sut.SelectedFilters);
        Assert.Single(sut.SelectedGenreFilters);
        Assert.Single(sut.SelectedTagFilters);
    }

    [Fact(DisplayName = "Save should persist all current selections back to AppOptions")]
    public void Save_ShouldPersistSelectionsToAppOptions()
    {
        // Arrange
        Mock<IAppOptions> options = CreateOptionsMock();
        ArtistsStateManager sut = new(options.Object)
        {
            GroupBy = GroupingConstants.Country,
            SelectedFilters = new() { "f1" },
            SelectedGenreFilters = new() { 7 },
            SelectedTagFilters = new() { "tag" }
        };

        // Act
        sut.Save();

        // Assert
        Assert.Equal(GroupingConstants.Country, options.Object.ArtistsGroupBy);
        Assert.Equal(new[] { "f1" }, options.Object.ArtistsFilterBy.ToArray());
        Assert.Equal(new long[] { 7 }, options.Object.ArtistsFilterByGenresId.ToArray());
        Assert.Equal(new[] { "tag" }, options.Object.ArtistsFilterByTags.ToArray());
    }

    private static (Mock<IAppOptions> Options, ArtistsStateManager Sut) CreateArtists(List<long> genres, List<string> tags)
    {
        Mock<IAppOptions> options = new();
        options.SetupProperty(o => o.ArtistsGroupBy, string.Empty);
        options.SetupProperty(o => o.ArtistsFilterBy, new List<string>());
        options.SetupProperty(o => o.ArtistsFilterByGenresId, genres);
        options.SetupProperty(o => o.ArtistsFilterByTags, tags);
        ArtistsStateManager sut = new(options.Object);
        sut.Load();

        return (options, sut);
    }

    [Fact(DisplayName = "prune_genre_filters_removes_unknown_genre_ids")]
    public void PruneGenreFilters_RemovesUnknownIds()
    {
        // Arrange
        (Mock<IAppOptions> options, ArtistsStateManager sut) = CreateArtists([1, 99], []);

        // Act
        bool removed = sut.PruneGenreFilters([1, 2]);

        // Assert
        Assert.True(removed);
        Assert.Equal(new long[] { 1 }, sut.SelectedGenreFilters.ToArray());
        Assert.Equal(new long[] { 1 }, options.Object.ArtistsFilterByGenresId.ToArray());
    }

    [Fact(DisplayName = "prune_tag_filters_removes_unknown_tags")]
    public void PruneTagFilters_RemovesUnknownTags()
    {
        // Arrange
        (Mock<IAppOptions> options, ArtistsStateManager sut) = CreateArtists([], ["rock", "gone"]);

        // Act
        bool removed = sut.PruneTagFilters(["rock"]);

        // Assert
        Assert.True(removed);
        Assert.Equal(new[] { "rock" }, sut.SelectedTagFilters.ToArray());
        Assert.Equal(new[] { "rock" }, options.Object.ArtistsFilterByTags.ToArray());
    }
}