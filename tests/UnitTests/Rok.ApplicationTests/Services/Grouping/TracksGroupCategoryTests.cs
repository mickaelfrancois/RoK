using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services.Grouping;

namespace Rok.ApplicationTests.Services.Grouping;

public class TracksGroupCategoryTests
{
    private static TracksGroupCategory CreateService(IResourceService? resource = null) => new(resource ?? Mock.Of<IResourceService>());

    private sealed class FakeTrack : IGroupableTrack
    {
        public string Title { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string? GenreName { get; set; }
        public int Score { get; set; }
        public int? TrackNumber { get; set; }
        public string? CountryCode { get; set; }
        public DateTime CreatDate { get; set; }
        public DateTime? LastListen { get; set; }
        public int ListenCount { get; set; }
    }

    [Fact(DisplayName = "Strategy Title should group tracks by first letter of title")]
    public void Strategy_Title_ShouldGroupByTitleFirstLetter()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "Anthem" },
            new FakeTrack { Title = "Beat" }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Title, input).ToList();

        // Assert
        Assert.Equal(new[] { "A", "B" }, groups.Select(g => g.Title).ToArray());
    }

    [Fact(DisplayName = "Strategy Artist should group tracks by artist name first letter and order by artist then album then track number")]
    public void Strategy_Artist_ShouldGroupAndSortHierarchically()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "T1", ArtistName = "AAA", AlbumName = "Z", TrackNumber = 2 },
            new FakeTrack { Title = "T2", ArtistName = "AAA", AlbumName = "Z", TrackNumber = 1 },
            new FakeTrack { Title = "T3", ArtistName = "BBB", AlbumName = "X", TrackNumber = 1 }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Artist, input).ToList();

        // Assert
        Assert.Equal(new[] { "A", "B" }, groups.Select(g => g.Title).ToArray());
        Assert.Equal(new[] { "T2", "T1" }, groups[0].Items.Select(t => t.Title).ToArray());
    }

    [Fact(DisplayName = "Strategy Album should group tracks by album first letter and order by album then track number")]
    public void Strategy_Album_ShouldGroupByAlbumFirstLetterAndSort()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "T1", AlbumName = "Apex", TrackNumber = 3 },
            new FakeTrack { Title = "T2", AlbumName = "Apex", TrackNumber = 1 },
            new FakeTrack { Title = "T3", AlbumName = "Beam", TrackNumber = 1 }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Album, input).ToList();

        // Assert
        Assert.Equal(new[] { "A", "B" }, groups.Select(g => g.Title).ToArray());
        Assert.Equal(new[] { "T2", "T1" }, groups[0].Items.Select(t => t.Title).ToArray());
    }

    [Fact(DisplayName = "Strategy Genre should bucket null or empty genre into the placeholder group")]
    public void Strategy_Genre_ShouldBucketNullGenreAsPlaceholder()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "T1", GenreName = "Rock" },
            new FakeTrack { Title = "T2", GenreName = null },
            new FakeTrack { Title = "T3", GenreName = string.Empty }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Genre, input).ToList();

        // Assert
        Assert.Contains(groups, g => g.Title == "#123" && g.Items.Count == 2);
        Assert.Contains(groups, g => g.Title == "Rock" && g.Items.Count == 1);
    }

    [Fact(DisplayName = "Strategy Score should group tracks by score and order groups descending")]
    public void Strategy_Score_ShouldGroupByScoreOrderedDescending()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "T1", Score = 1 },
            new FakeTrack { Title = "T2", Score = 5 },
            new FakeTrack { Title = "T3", Score = 5 }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Score, input).ToList();

        // Assert
        Assert.Equal(new[] { "5", "1" }, groups.Select(g => g.Title).ToArray());
        Assert.Equal(2, groups[0].Items.Count);
    }

    [Fact(DisplayName = "Strategy Country should bucket tracks with empty country code into the placeholder")]
    public void Strategy_Country_ShouldBucketNullCodeAsPlaceholder()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "T1", CountryCode = "FR" },
            new FakeTrack { Title = "T2", CountryCode = null }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.Country, input).ToList();

        // Assert
        Assert.Contains(groups, g => g.Title == "#123");
        Assert.Contains(groups, g => g.Title == "FR");
    }

    [Fact(DisplayName = "Strategy LastListen should sort tracks by descending last listen")]
    public void Strategy_LastListen_ShouldSortDescending()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        DateTime now = DateTime.Now;
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "Old", LastListen = now.AddDays(-30) },
            new FakeTrack { Title = "Recent", LastListen = now.AddDays(-1) }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.LastListen, input).ToList();

        // Assert
        Assert.Single(groups);
        Assert.Equal("Recent", groups[0].Items[0].Title);
    }

    [Fact(DisplayName = "Strategy ListenCount should sort tracks by descending listen count")]
    public void Strategy_ListenCount_ShouldSortDescending()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "Few", ListenCount = 1 },
            new FakeTrack { Title = "Many", ListenCount = 9 }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.ListenCount, input).ToList();

        // Assert
        Assert.Single(groups);
        Assert.Equal("Many", groups[0].Items[0].Title);
    }

    [Fact(DisplayName = "Strategy CreatDate should bucket old tracks separately from recent ones")]
    public void Strategy_CreatDate_ShouldBucketOldSeparately()
    {
        // Arrange
        TracksGroupCategory sut = CreateService();
        List<IGroupableTrack> input = new()
        {
            new FakeTrack { Title = "Recent", CreatDate = DateTime.Now },
            new FakeTrack { Title = "Old", CreatDate = DateTime.Now.AddYears(-3) }
        };

        // Act
        List<TrackGroupResult> groups = sut.GetGroupedItems(GroupingConstants.CreatDate, input).ToList();

        // Assert
        Assert.Equal(2, groups.Count);
    }

    [Theory(DisplayName = "GetGroupByLabel should request the expected resource key for each known group")]
    [InlineData(GroupingConstants.Title, "tracksViewGroupByTitle")]
    [InlineData(GroupingConstants.Country, "tracksViewGroupByCountry")]
    [InlineData(GroupingConstants.CreatDate, "tracksViewGroupByCreatDate")]
    [InlineData(GroupingConstants.LastListen, "tracksViewGroupByLastListen")]
    [InlineData(GroupingConstants.ListenCount, "tracksViewGroupByListenCount")]
    [InlineData(GroupingConstants.Artist, "tracksViewGroupByArtist")]
    [InlineData(GroupingConstants.Album, "tracksViewGroupByAlbum")]
    [InlineData(GroupingConstants.Genre, "tracksViewGroupByGenre")]
    [InlineData(GroupingConstants.Score, "tracksViewGroupByScore")]
    public void GetGroupByLabel_ShouldRequestExpectedResourceKey(string groupBy, string expectedResourceKey)
    {
        // Arrange
        Mock<IResourceService> resource = new();
        resource.Setup(r => r.GetString(expectedResourceKey)).Returns("LABEL");
        TracksGroupCategory sut = CreateService(resource.Object);

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
        TracksGroupCategory sut = CreateService();

        // Act
        string label = sut.GetGroupByLabel("UNMAPPED");

        // Assert
        Assert.Equal("UNMAPPED", label);
    }
}