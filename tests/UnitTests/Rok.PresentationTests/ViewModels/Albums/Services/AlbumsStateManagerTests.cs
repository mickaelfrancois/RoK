using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Grouping;
using Rok.ViewModels.Albums.Services;

namespace Rok.PresentationTests.ViewModels.Albums.Services;

public class AlbumsStateManagerTests
{
    [Fact(DisplayName = "Load should fall back to the default GroupBy when no value is stored")]
    public void Load_ShouldUseDefaultGroupBy_WhenStoredIsEmpty()
    {
        // Arrange
        Mock<IAppOptions> options = new();
        options.SetupProperty(o => o.AlbumsGroupBy, string.Empty);
        options.SetupProperty(o => o.AlbumsFilterBy, new List<string>());
        options.SetupProperty(o => o.AlbumsFilterByGenresId, new List<long>());
        options.SetupProperty(o => o.AlbumsFilterByTags, new List<string>());
        AlbumsStateManager sut = new(options.Object);

        // Act
        sut.Load();

        // Assert
        Assert.Equal(GroupingConstants.Album, sut.GroupBy);
    }

    [Fact(DisplayName = "Load should use the stored GroupBy when present")]
    public void Load_ShouldUseStoredGroupBy_WhenPresent()
    {
        // Arrange
        Mock<IAppOptions> options = new();
        options.SetupProperty(o => o.AlbumsGroupBy, GroupingConstants.Year);
        options.SetupProperty(o => o.AlbumsFilterBy, new List<string> { "fav" });
        options.SetupProperty(o => o.AlbumsFilterByGenresId, new List<long> { 1, 2 });
        options.SetupProperty(o => o.AlbumsFilterByTags, new List<string> { "rock" });
        AlbumsStateManager sut = new(options.Object);

        // Act
        sut.Load();

        // Assert
        Assert.Equal(GroupingConstants.Year, sut.GroupBy);
        Assert.Equal(new[] { "fav" }, sut.SelectedFilters.ToArray());
        Assert.Equal(new long[] { 1, 2 }, sut.SelectedGenreFilters.ToArray());
        Assert.Equal(new[] { "rock" }, sut.SelectedTagFilters.ToArray());
    }

    [Fact(DisplayName = "Save should persist all current selections back to AppOptions")]
    public void Save_ShouldPersistSelectionsToAppOptions()
    {
        // Arrange
        Mock<IAppOptions> options = new();
        options.SetupProperty(o => o.AlbumsGroupBy, string.Empty);
        options.SetupProperty(o => o.AlbumsFilterBy, new List<string>());
        options.SetupProperty(o => o.AlbumsFilterByGenresId, new List<long>());
        options.SetupProperty(o => o.AlbumsFilterByTags, new List<string>());
        AlbumsStateManager sut = new(options.Object)
        {
            GroupBy = GroupingConstants.Decade,
            SelectedFilters = new() { "f1" },
            SelectedGenreFilters = new() { 7 },
            SelectedTagFilters = new() { "tag" }
        };

        // Act
        sut.Save();

        // Assert
        Assert.Equal(GroupingConstants.Decade, options.Object.AlbumsGroupBy);
        Assert.Equal(new[] { "f1" }, options.Object.AlbumsFilterBy.ToArray());
        Assert.Equal(new long[] { 7 }, options.Object.AlbumsFilterByGenresId.ToArray());
        Assert.Equal(new[] { "tag" }, options.Object.AlbumsFilterByTags.ToArray());
    }

    [Fact(DisplayName = "SaveGridView and GetGridView should round-trip via AppOptions")]
    public void GridView_ShouldRoundTripThroughAppOptions()
    {
        // Arrange
        Mock<IAppOptions> options = new();
        options.SetupProperty(o => o.IsGridView, false);
        options.SetupProperty(o => o.AlbumsGroupBy, string.Empty);
        options.SetupProperty(o => o.AlbumsFilterBy, new List<string>());
        options.SetupProperty(o => o.AlbumsFilterByGenresId, new List<long>());
        options.SetupProperty(o => o.AlbumsFilterByTags, new List<string>());
        AlbumsStateManager sut = new(options.Object);

        // Act
        sut.SaveGridView(true);

        // Assert
        Assert.True(sut.GetGridView());
        Assert.True(options.Object.IsGridView);
    }
}