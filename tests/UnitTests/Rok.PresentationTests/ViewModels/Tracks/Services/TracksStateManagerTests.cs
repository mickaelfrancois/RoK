using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Grouping;
using Rok.ViewModels.Tracks.Services;

namespace Rok.PresentationTests.ViewModels.Tracks.Services;

public class TracksStateManagerTests
{
    private static Mock<IAppOptions> CreateOptionsMock(string groupBy = "", List<string>? filters = null, List<long>? genres = null, List<string>? tags = null)
    {
        Mock<IAppOptions> options = new();
        options.SetupProperty(o => o.TracksGroupBy, groupBy);
        options.SetupProperty(o => o.TracksFilterBy, filters ?? new List<string>());
        options.SetupProperty(o => o.TracksFilterByGenresId, genres ?? new List<long>());
        options.SetupProperty(o => o.TracksFilterByTags, tags ?? new List<string>());
        return options;
    }

    [Fact(DisplayName = "Load should fall back to Title GroupBy when no value is stored")]
    public void Load_ShouldUseDefaultGroupBy_WhenStoredIsEmpty()
    {
        // Arrange
        TracksStateManager sut = new(CreateOptionsMock().Object);

        // Act
        sut.Load();

        // Assert
        Assert.Equal(GroupingConstants.Title, sut.GroupBy);
    }

    [Fact(DisplayName = "Load should use the stored GroupBy and filters when present")]
    public void Load_ShouldUseStoredValues_WhenPresent()
    {
        // Arrange
        TracksStateManager sut = new(CreateOptionsMock(
            groupBy: GroupingConstants.Score,
            filters: new() { "fav" },
            genres: new() { 5 },
            tags: new() { "rock" }).Object);

        // Act
        sut.Load();

        // Assert
        Assert.Equal(GroupingConstants.Score, sut.GroupBy);
        Assert.Single(sut.SelectedFilters);
        Assert.Single(sut.SelectedGenreFilters);
        Assert.Single(sut.SelectedTagFilters);
    }

    [Fact(DisplayName = "Save should persist all current selections back to AppOptions")]
    public void Save_ShouldPersistSelectionsToAppOptions()
    {
        // Arrange
        Mock<IAppOptions> options = CreateOptionsMock();
        TracksStateManager sut = new(options.Object)
        {
            GroupBy = GroupingConstants.Genre,
            SelectedFilters = new() { "f1" },
            SelectedGenreFilters = new() { 7 },
            SelectedTagFilters = new() { "tag" }
        };

        // Act
        sut.Save();

        // Assert
        Assert.Equal(GroupingConstants.Genre, options.Object.TracksGroupBy);
        Assert.Equal(new[] { "f1" }, options.Object.TracksFilterBy.ToArray());
        Assert.Equal(new long[] { 7 }, options.Object.TracksFilterByGenresId.ToArray());
        Assert.Equal(new[] { "tag" }, options.Object.TracksFilterByTags.ToArray());
    }
}