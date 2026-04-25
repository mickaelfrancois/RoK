using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Grouping;

namespace Rok.ApplicationTests.Services.Grouping;

public class ArtistsGroupCategoryTests
{
    private static ArtistsGroupCategory CreateService(IResourceService? resource = null) => new(resource ?? Mock.Of<IResourceService>());

    private sealed class FakeArtist : IGroupableArtist
    {
        public string Name { get; set; } = string.Empty;
        public int? YearMini { get; set; }
        public string? CountryCode { get; set; }
        public DateTime CreatDate { get; set; }
        public DateTime? LastListen { get; set; }
        public int ListenCount { get; set; }
    }

    [Fact(DisplayName = "Strategy None should group artists by first letter of name")]
    public void Strategy_None_ShouldGroupByFirstLetter()
    {
        // Arrange
        ArtistsGroupCategory sut = CreateService();
        List<IGroupableArtist> input = new()
        {
            new FakeArtist { Name = "Beatles" },
            new FakeArtist { Name = "ACDC" }
        };

        // Act
        List<ArtistGroupResult> groups = sut.GetGroupedItems(GroupingConstants.None, input).ToList();

        // Assert
        Assert.Equal(new[] { "A", "B" }, groups.Select(g => g.Title).ToArray());
    }

    [Fact(DisplayName = "Strategy Year should reuse decade grouping for artists")]
    public void Strategy_Year_ShouldReuseDecadeGrouping()
    {
        // Arrange
        ArtistsGroupCategory sut = CreateService();
        List<IGroupableArtist> input = new()
        {
            new FakeArtist { Name = "A", YearMini = 2003 },
            new FakeArtist { Name = "B", YearMini = 2024 }
        };

        // Act
        List<ArtistGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Year, input).ToList();

        // Assert
        Assert.Equal(new[] { "2020", "2000" }, groups.Select(g => g.Title).ToArray());
    }

    [Fact(DisplayName = "Strategy Country should fall back to placeholder for null code")]
    public void Strategy_Country_ShouldFallBackToPlaceholder()
    {
        // Arrange
        ArtistsGroupCategory sut = CreateService();
        List<IGroupableArtist> input = new()
        {
            new FakeArtist { Name = "A", CountryCode = "US" },
            new FakeArtist { Name = "B", CountryCode = null }
        };

        // Act
        List<ArtistGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Country, input).ToList();

        // Assert
        Assert.Contains(groups, g => g.Title == "#123");
        Assert.Contains(groups, g => g.Title == "US");
    }

    [Fact(DisplayName = "Strategy LastListen should sort artists by descending last listen")]
    public void Strategy_LastListen_ShouldSortDescending()
    {
        // Arrange
        ArtistsGroupCategory sut = CreateService();
        DateTime now = DateTime.Now;
        List<IGroupableArtist> input = new()
        {
            new FakeArtist { Name = "Old", LastListen = now.AddDays(-30) },
            new FakeArtist { Name = "Recent", LastListen = now.AddDays(-1) }
        };

        // Act
        List<ArtistGroupResult> groups = sut.GetGroupedItems(GroupingConstants.LastListen, input).ToList();

        // Assert
        Assert.Single(groups);
        Assert.Equal("Recent", groups[0].Items[0].Name);
    }

    [Fact(DisplayName = "Strategy ListenCount should sort artists by descending listen count")]
    public void Strategy_ListenCount_ShouldSortDescending()
    {
        // Arrange
        ArtistsGroupCategory sut = CreateService();
        List<IGroupableArtist> input = new()
        {
            new FakeArtist { Name = "Few", ListenCount = 3 },
            new FakeArtist { Name = "Many", ListenCount = 50 }
        };

        // Act
        List<ArtistGroupResult> groups = sut.GetGroupedItems(GroupingConstants.ListenCount, input).ToList();

        // Assert
        Assert.Single(groups);
        Assert.Equal("Many", groups[0].Items[0].Name);
    }

    [Fact(DisplayName = "Strategy CreatDate should bucket recent artists separately from old ones")]
    public void Strategy_CreatDate_ShouldBucketRecentSeparately()
    {
        // Arrange
        ArtistsGroupCategory sut = CreateService();
        List<IGroupableArtist> input = new()
        {
            new FakeArtist { Name = "Recent", CreatDate = DateTime.Now },
            new FakeArtist { Name = "Old", CreatDate = DateTime.Now.AddYears(-3) }
        };

        // Act
        List<ArtistGroupResult> groups = sut.GetGroupedItems(GroupingConstants.CreatDate, input).ToList();

        // Assert
        Assert.Equal(2, groups.Count);
    }

    [Theory(DisplayName = "GetGroupByLabel should request the expected resource key for each known group")]
    [InlineData(GroupingConstants.Year, "artistsViewGroupByYear")]
    [InlineData(GroupingConstants.Decade, "artistsViewGroupByDecade")]
    [InlineData(GroupingConstants.Country, "artistsViewGroupByCountry")]
    [InlineData(GroupingConstants.CreatDate, "artistsViewGroupByCreatDate")]
    [InlineData(GroupingConstants.Artist, "artistsViewGroupByArtist")]
    [InlineData(GroupingConstants.LastListen, "artistsViewGroupByLastListen")]
    [InlineData(GroupingConstants.ListenCount, "artistsViewGroupByListenCount")]
    public void GetGroupByLabel_ShouldRequestExpectedResourceKey(string groupBy, string expectedResourceKey)
    {
        // Arrange
        Mock<IResourceService> resource = new();
        resource.Setup(r => r.GetString(expectedResourceKey)).Returns("LABEL");
        ArtistsGroupCategory sut = CreateService(resource.Object);

        // Act
        string label = sut.GetGroupByLabel(groupBy);

        // Assert
        Assert.Equal("LABEL", label);
        resource.Verify(r => r.GetString(expectedResourceKey), Times.Once);
    }

    [Fact(DisplayName = "GetGroupByLabel should return the key itself when no mapping exists")]
    public void GetGroupByLabel_WithUnknownKey_ShouldReturnTheKeyItself()
    {
        // Arrange
        ArtistsGroupCategory sut = CreateService();

        // Act
        string label = sut.GetGroupByLabel("UNMAPPED");

        // Assert
        Assert.Equal("UNMAPPED", label);
    }
}
