using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Grouping;

namespace Rok.ApplicationTests.Services.Grouping;

public class AlbumsGroupCategoryTests
{
    private static AlbumsGroupCategory CreateService(IResourceService? resource = null) => new(resource ?? Mock.Of<IResourceService>());

    private sealed class FakeAlbum : IGroupableAlbum
    {
        public string Name { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string? CountryCode { get; set; }
        public DateTime CreatDate { get; set; }
        public DateTime? LastListen { get; set; }
        public int ListenCount { get; set; }
    }

    [Fact(DisplayName = "GetGroupedItems should throw when group key is unknown")]
    public void GetGroupedItems_WithUnknownKey_ShouldThrow()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.GetGroupedItems("UNKNOWN", new List<IGroupableAlbum>()).ToList());
    }

    [Fact(DisplayName = "GetGroupedItems with empty groupBy should default to None strategy and group by name")]
    public void GetGroupedItems_WithEmptyGroupBy_ShouldDefaultToNoneStrategy()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        List<IGroupableAlbum> input = new() { new FakeAlbum { Name = "Apple" }, new FakeAlbum { Name = "Banana" } };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(string.Empty, input).ToList();

        // Assert
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Title == "A");
        Assert.Contains(groups, g => g.Title == "B");
    }

    [Fact(DisplayName = "GroupByName should bucket items by first letter of name and order alphabetically")]
    public void Strategy_None_ShouldGroupByFirstLetter()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        List<IGroupableAlbum> input = new()
        {
            new FakeAlbum { Name = "Banana" },
            new FakeAlbum { Name = "Apple" },
            new FakeAlbum { Name = "Avocado" }
        };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(GroupingConstants.None, input).ToList();

        // Assert
        Assert.Equal("A", groups[0].Title);
        Assert.Equal(2, groups[0].Items.Count);
        Assert.Equal("Apple", groups[0].Items[0].Name);
        Assert.Equal("Avocado", groups[0].Items[1].Name);
        Assert.Equal("B", groups[1].Title);
    }

    [Fact(DisplayName = "GroupByDecade should bucket albums by decade and exclude items without year")]
    public void Strategy_Decade_ShouldBucketByDecadeAndExcludeNullYears()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        List<IGroupableAlbum> input = new()
        {
            new FakeAlbum { Name = "A1", Year = 2003 },
            new FakeAlbum { Name = "A2", Year = 2024 },
            new FakeAlbum { Name = "A3", Year = null }
        };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Decade, input).ToList();

        // Assert
        Assert.Equal(new[] { "2020", "2000" }, groups.Select(g => g.Title).ToArray());
        Assert.DoesNotContain(groups.SelectMany(g => g.Items), a => a.Year is null);
    }

    [Fact(DisplayName = "GroupByYear should bucket albums by year and exclude null years")]
    public void Strategy_Year_ShouldBucketByYearAndExcludeNullYears()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        List<IGroupableAlbum> input = new()
        {
            new FakeAlbum { Name = "A1", Year = 2003 },
            new FakeAlbum { Name = "A2", Year = 2024 },
            new FakeAlbum { Name = "A3", Year = null }
        };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Year, input).ToList();

        // Assert
        Assert.Equal(new[] { "2024", "2003" }, groups.Select(g => g.Title).ToArray());
    }

    [Fact(DisplayName = "GroupByCountry should bucket null country code into the hash placeholder")]
    public void Strategy_Country_ShouldBucketNullCodeAsPlaceholder()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        List<IGroupableAlbum> input = new()
        {
            new FakeAlbum { Name = "A1", CountryCode = "FR" },
            new FakeAlbum { Name = "A2", CountryCode = null },
            new FakeAlbum { Name = "A3", CountryCode = "" }
        };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Country, input).ToList();

        // Assert
        Assert.Contains(groups, g => g.Title == "#123" && g.Items.Count == 2);
        Assert.Contains(groups, g => g.Title == "FR" && g.Items.Count == 1);
    }

    [Fact(DisplayName = "GroupByCreatDate should bucket items older than one year into the legacy bucket")]
    public void Strategy_CreatDate_ShouldBucketOldItemsTogether()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        DateTime now = DateTime.Now;
        DateTime oldDate = now.AddYears(-2);
        List<IGroupableAlbum> input = new()
        {
            new FakeAlbum { Name = "Recent", CreatDate = now },
            new FakeAlbum { Name = "Older", CreatDate = oldDate }
        };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(GroupingConstants.CreatDate, input).ToList();

        // Assert
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Title.StartsWith("<"));
    }

    [Fact(DisplayName = "SortByLastListen should produce a single group ordered by descending last listen")]
    public void Strategy_LastListen_ShouldReturnSingleSortedGroup()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        DateTime now = DateTime.Now;
        List<IGroupableAlbum> input = new()
        {
            new FakeAlbum { Name = "Old", LastListen = now.AddDays(-10) },
            new FakeAlbum { Name = "Recent", LastListen = now.AddDays(-1) }
        };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(GroupingConstants.LastListen, input).ToList();

        // Assert
        Assert.Single(groups);
        Assert.Equal(string.Empty, groups[0].Title);
        Assert.Equal("Recent", groups[0].Items[0].Name);
        Assert.Equal("Old", groups[0].Items[1].Name);
    }

    [Fact(DisplayName = "SortByListenCount should produce a single group ordered by descending listen count")]
    public void Strategy_ListenCount_ShouldReturnSingleSortedGroup()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        List<IGroupableAlbum> input = new()
        {
            new FakeAlbum { Name = "Few", ListenCount = 1 },
            new FakeAlbum { Name = "Many", ListenCount = 99 }
        };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(GroupingConstants.ListenCount, input).ToList();

        // Assert
        Assert.Single(groups);
        Assert.Equal("Many", groups[0].Items[0].Name);
        Assert.Equal("Few", groups[0].Items[1].Name);
    }

    [Fact(DisplayName = "Strategy Artist should group albums by artist first letter")]
    public void Strategy_Artist_ShouldGroupByArtistFirstLetter()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();
        List<IGroupableAlbum> input = new()
        {
            new FakeAlbum { Name = "A1", ArtistName = "Beatles" },
            new FakeAlbum { Name = "A2", ArtistName = "ACDC" }
        };

        // Act
        List<AlbumGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Artist, input).ToList();

        // Assert
        Assert.Equal(new[] { "A", "B" }, groups.Select(g => g.Title).ToArray());
    }

    [Theory(DisplayName = "GetGroupByLabel should request the expected resource key for each known group")]
    [InlineData(GroupingConstants.Decade, "albumsViewGroupByYear")]
    [InlineData(GroupingConstants.Year, "albumsViewGroupByYear")]
    [InlineData(GroupingConstants.Country, "albumsViewGroupByCountry")]
    [InlineData(GroupingConstants.CreatDate, "albumsViewGroupByCreatDate")]
    [InlineData(GroupingConstants.Artist, "albumsViewGroupByArtist")]
    [InlineData(GroupingConstants.Album, "albumsViewGroupByAlbum")]
    [InlineData(GroupingConstants.LastListen, "albumsViewGroupByLastListen")]
    [InlineData(GroupingConstants.ListenCount, "albumsViewGroupByListenCount")]
    public void GetGroupByLabel_ShouldRequestExpectedResourceKey(string groupBy, string expectedResourceKey)
    {
        // Arrange
        Mock<IResourceService> resource = new();
        resource.Setup(r => r.GetString(expectedResourceKey)).Returns("LABEL");
        AlbumsGroupCategory sut = CreateService(resource.Object);

        // Act
        string label = sut.GetGroupByLabel(groupBy);

        // Assert
        Assert.Equal("LABEL", label);
        resource.Verify(r => r.GetString(expectedResourceKey), Times.Once);
    }

    [Fact(DisplayName = "GetGroupByLabel should return the group key as label when no mapping exists")]
    public void GetGroupByLabel_WithUnknownKey_ShouldReturnTheKeyItself()
    {
        // Arrange
        AlbumsGroupCategory sut = CreateService();

        // Act
        string label = sut.GetGroupByLabel("UNMAPPED");

        // Assert
        Assert.Equal("UNMAPPED", label);
    }
}
