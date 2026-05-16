using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Requests;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;
using Rok.Shared.Enums;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class PlaylistTrackGenerateRepositoryTests
{
    private static PlaylistTrackGenerateRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<PlaylistTrackGenerateRepository>.Instance, TimeProvider.System);

    private static GeneratePlaylistTracksRequest BuildQuery(int count, SmartPlaylistSelectBy sort = SmartPlaylistSelectBy.Random, params PlaylistFilterDto[] filters) =>
        new()
        {
            PlaylistTrackCount = count,
            Group = new PlaylistGroupDto
            {
                SortBy = sort,
                Filters = filters.ToList()
            }
        };

    [Fact(DisplayName = "Generate should respect the requested track count limit")]
    public async Task Generate_ShouldRespectRequestedTrackCountLimit()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(2));

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact(DisplayName = "Generate should include related album artist and genre names")]
    public async Task Generate_ShouldIncludeRelatedAlbumArtistAndGenreNames()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10));

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, t => Assert.False(string.IsNullOrEmpty(t.AlbumName)));
        Assert.All(result, t => Assert.False(string.IsNullOrEmpty(t.ArtistName)));
    }

    [Fact(DisplayName = "Generate should filter tracks by a numeric comparison on albums")]
    public async Task Generate_ShouldFilterTracks_ByNumericComparisonOnAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Albums,
            Field = SmartPlaylistField.ListenCount,
            FieldType = SmartPlaylistFieldType.Int,
            Operator = SmartPlaylistOperator.GreaterThan,
            Value = "3"
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result[0].AlbumId);
    }

    [Fact(DisplayName = "Generate should apply a name contains filter on albums")]
    public async Task Generate_ShouldApplyNameContainsFilter_OnAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Albums,
            Field = SmartPlaylistField.Name,
            FieldType = SmartPlaylistFieldType.String,
            Operator = SmartPlaylistOperator.Contains,
            Value = "First"
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, t => Assert.Contains("First", t.AlbumName));
    }

    [Fact(DisplayName = "Generate sorted by Newest should order tracks by creation date descending")]
    public async Task Generate_SortedByNewest_ShouldOrderTracksByCreationDateDescending()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Newest));

        // Assert
        List<DateTime> dates = result.Select(t => t.CreatDate).ToList();
        for (int i = 0; i < dates.Count - 1; i++)
        {
            Assert.True(dates[i] >= dates[i + 1]);
        }
    }

    [Theory(DisplayName = "Generate should apply name filter operators on albums")]
    [InlineData(SmartPlaylistOperator.Equals, "The First Album", new long[] { 1, 2 })]
    [InlineData(SmartPlaylistOperator.NotEquals, "The First Album", new long[] { 3 })]
    [InlineData(SmartPlaylistOperator.NotContains, "First", new long[] { 3 })]
    [InlineData(SmartPlaylistOperator.StartsWith, "The", new long[] { 1, 2 })]
    [InlineData(SmartPlaylistOperator.EndsWith, "Sounds", new long[] { 3 })]
    public async Task Generate_NameFilterOperators_OnAlbums(SmartPlaylistOperator op, string value, long[] expectedTrackIds)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Albums,
            Field = SmartPlaylistField.Name,
            FieldType = SmartPlaylistFieldType.String,
            Operator = op,
            Value = value
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Equal(expectedTrackIds.OrderBy(x => x), result.Select(t => t.Id).OrderBy(x => x));
    }

    [Fact(DisplayName = "Generate should combine multiple name filters within a group with OR")]
    public async Task Generate_MultipleNameFilters_ShouldUseOrWithinGroup()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        PlaylistFilterDto first = new()
        {
            Entity = SmartPlaylistEntity.Albums,
            Field = SmartPlaylistField.Name,
            FieldType = SmartPlaylistFieldType.String,
            Operator = SmartPlaylistOperator.Equals,
            Value = "The First Album"
        };
        PlaylistFilterDto second = new()
        {
            Entity = SmartPlaylistEntity.Albums,
            Field = SmartPlaylistField.Name,
            FieldType = SmartPlaylistFieldType.String,
            Operator = SmartPlaylistOperator.Equals,
            Value = "Second Sounds"
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, first, second));

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact(DisplayName = "Generate should filter by artist name")]
    public async Task Generate_NameFilter_OnArtists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Artists,
            Field = SmartPlaylistField.Name,
            FieldType = SmartPlaylistFieldType.String,
            Operator = SmartPlaylistOperator.Equals,
            Value = "Artist B"
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result[0].ArtistId);
    }

    [Fact(DisplayName = "Generate should filter by genre name")]
    public async Task Generate_NameFilter_OnGenres()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET genreId = 1 WHERE id IN (1, 2)");
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET genreId = 2 WHERE id = 3");
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Genres,
            Field = SmartPlaylistField.Name,
            FieldType = SmartPlaylistFieldType.String,
            Operator = SmartPlaylistOperator.Equals,
            Value = "Jazz"
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact(DisplayName = "Generate should filter by country code")]
    public async Task Generate_NameFilter_OnCountries()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE Artists SET countryId = 1 WHERE id = 1");
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Countries,
            Field = SmartPlaylistField.Code,
            FieldType = SmartPlaylistFieldType.String,
            Operator = SmartPlaylistOperator.Equals,
            Value = "FR"
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(1, t.ArtistId));
    }

    [Theory(DisplayName = "Generate should apply int filter operators on album listen count")]
    [InlineData(SmartPlaylistOperator.LessThan, 5, 2)]
    [InlineData(SmartPlaylistOperator.Equals, 5, 1)]
    [InlineData(SmartPlaylistOperator.NotEquals, 0, 1)]
    public async Task Generate_IntFilterOperators(SmartPlaylistOperator op, int value, int expectedCount)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Albums,
            Field = SmartPlaylistField.ListenCount,
            FieldType = SmartPlaylistFieldType.Int,
            Operator = op,
            Value = value.ToString()
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Equal(expectedCount, result.Count);
    }

    [Theory(DisplayName = "Generate should apply bool filter operators on album favorite flag")]
    [InlineData(SmartPlaylistOperator.Equals, "True", 1)]
    [InlineData(SmartPlaylistOperator.Equals, "False", 2)]
    [InlineData(SmartPlaylistOperator.NotEquals, "True", 2)]
    public async Task Generate_BoolFilterOperators(SmartPlaylistOperator op, string value, int expectedCount)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Albums,
            Field = SmartPlaylistField.IsFavorite,
            FieldType = SmartPlaylistFieldType.Bool,
            Operator = op,
            Value = value
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Equal(expectedCount, result.Count);
    }

    [Theory(DisplayName = "Generate should apply date filter operators on track creation date")]
    [InlineData(SmartPlaylistOperator.GreaterThan, -1, 3)]
    [InlineData(SmartPlaylistOperator.LessThan, 1, 3)]
    [InlineData(SmartPlaylistOperator.Equals, -10, 0)]
    [InlineData(SmartPlaylistOperator.NotEquals, -10, 3)]
    public async Task Generate_DateFilterOperators(SmartPlaylistOperator op, int dayOffset, int expectedCount)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        DateOnly reference = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(dayOffset);
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Tracks,
            Field = SmartPlaylistField.CreatDate,
            FieldType = SmartPlaylistFieldType.Date,
            Operator = op,
            Value = reference.ToString("yyyy/MM/dd")
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Equal(expectedCount, result.Count);
    }

    [Theory(DisplayName = "Generate should apply day filter operators with inverted comparison semantic")]
    [InlineData(SmartPlaylistOperator.LessThan, 7, 1)]
    [InlineData(SmartPlaylistOperator.GreaterThan, 7, 1)]
    [InlineData(SmartPlaylistOperator.Equals, 7, 0)]
    [InlineData(SmartPlaylistOperator.NotEquals, 7, 2)]
    public async Task Generate_DayFilterOperators(SmartPlaylistOperator op, int dayValue, int expectedCount)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET lastListen = @recent WHERE id = 1", new { recent = now.AddDays(-1) });
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET lastListen = @old WHERE id = 2", new { old = now.AddDays(-30) });
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Tracks,
            Field = SmartPlaylistField.LastListen,
            FieldType = SmartPlaylistFieldType.Day,
            Operator = op,
            Value = dayValue.ToString()
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Equal(expectedCount, result.Count);
    }

    [Fact(DisplayName = "Generate should apply between int filter on a numeric range")]
    public async Task Generate_BetweenIntFilter_ShouldFilterRange()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET score = 10 WHERE id = 1");
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET score = 50 WHERE id = 2");
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET score = 90 WHERE id = 3");
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Tracks,
            Field = SmartPlaylistField.Score,
            FieldType = SmartPlaylistFieldType.Int,
            Operator = SmartPlaylistOperator.Between,
            Value = "20",
            Value2 = "60"
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact(DisplayName = "Generate should apply between date filter on a date range")]
    public async Task Generate_BetweenDateFilter_ShouldFilterRange()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        PlaylistFilterDto filter = new()
        {
            Entity = SmartPlaylistEntity.Tracks,
            Field = SmartPlaylistField.CreatDate,
            FieldType = SmartPlaylistFieldType.Date,
            Operator = SmartPlaylistOperator.Between,
            Value = today.AddDays(-1).ToString("yyyy/MM/dd"),
            Value2 = today.AddDays(1).ToString("yyyy/MM/dd")
        };

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Random, filter));

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Theory(DisplayName = "Generate should support all sort options without error")]
    [InlineData(SmartPlaylistSelectBy.Random)]
    [InlineData(SmartPlaylistSelectBy.Oldest)]
    [InlineData(SmartPlaylistSelectBy.MostPlayed)]
    [InlineData(SmartPlaylistSelectBy.LeastPlayed)]
    [InlineData(SmartPlaylistSelectBy.HighestRated)]
    [InlineData(SmartPlaylistSelectBy.LowestRated)]
    [InlineData(SmartPlaylistSelectBy.MostRecent)]
    [InlineData(SmartPlaylistSelectBy.LeastRecent)]
    public async Task Generate_AllSortOptions_ShouldReturnTracks(SmartPlaylistSelectBy sort)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, sort));

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact(DisplayName = "Generate sorted by Oldest should order tracks by creation date ascending")]
    public async Task Generate_SortedByOldest_ShouldOrderByCreatDateAscending()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        DateTime baseDate = DateTime.UtcNow;
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET creatDate = @d WHERE id = 1", new { d = baseDate.AddDays(-3) });
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET creatDate = @d WHERE id = 2", new { d = baseDate.AddDays(-1) });
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET creatDate = @d WHERE id = 3", new { d = baseDate.AddDays(-2) });

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.Oldest));

        // Assert
        Assert.Equal(new long[] { 1, 3, 2 }, result.Select(t => t.Id).ToArray());
    }

    [Fact(DisplayName = "Generate sorted by MostPlayed should order tracks by listen count descending")]
    public async Task Generate_SortedByMostPlayed_ShouldOrderByListenCountDescending()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET listenCount = 5 WHERE id = 1");
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET listenCount = 20 WHERE id = 2");
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET listenCount = 10 WHERE id = 3");

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.MostPlayed));

        // Assert
        Assert.Equal(2, result[0].Id);
        Assert.Equal(3, result[1].Id);
        Assert.Equal(1, result[2].Id);
    }

    [Fact(DisplayName = "Generate sorted by HighestRated should order tracks by score descending")]
    public async Task Generate_SortedByHighestRated_ShouldOrderByScoreDescending()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackGenerateRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET score = 30 WHERE id = 1");
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET score = 90 WHERE id = 2");
        await fixture.Connection.ExecuteAsync("UPDATE Tracks SET score = 60 WHERE id = 3");

        // Act
        List<TrackEntity> result = await repo.GenerateAsync(BuildQuery(10, SmartPlaylistSelectBy.HighestRated));

        // Assert
        Assert.Equal(2, result[0].Id);
        Assert.Equal(3, result[1].Id);
        Assert.Equal(1, result[2].Id);
    }
}
