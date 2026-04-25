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
}
