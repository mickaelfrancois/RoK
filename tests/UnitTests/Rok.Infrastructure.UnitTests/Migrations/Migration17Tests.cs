using Dapper;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration17Tests
{
    private const string InsertTrackSql = """
        INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, artistId, albumId, genreId)
        VALUES (@id, @title, 100, 1000, 128, @musicFile, @now, 0, 0, 0, 0, @now, @artistId, @albumId, @genreId)
        """;

    [Fact(DisplayName = "Migration 17 should clear track references pointing to missing entities")]
    public void Migration17_ShouldClearTrackReferences_PointingToMissingEntities()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        fixture.Connection.Execute(InsertTrackSql, new
        {
            id = 99,
            title = "drifted",
            musicFile = "/f99",
            now = DateTime.UtcNow,
            artistId = 777,
            albumId = 888,
            genreId = 999
        });

        // Act
        new Migration17().Apply(fixture.Connection);

        // Assert
        Assert.Null(fixture.Connection.QuerySingle<long?>("SELECT artistId FROM Tracks WHERE id = 99"));
        Assert.Null(fixture.Connection.QuerySingle<long?>("SELECT albumId FROM Tracks WHERE id = 99"));
        Assert.Null(fixture.Connection.QuerySingle<long?>("SELECT genreId FROM Tracks WHERE id = 99"));
    }

    [Fact(DisplayName = "Migration 17 should keep track references pointing to existing entities")]
    public void Migration17_ShouldKeepTrackReferences_PointingToExistingEntities()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        fixture.Connection.Execute(InsertTrackSql, new
        {
            id = 98,
            title = "consistent",
            musicFile = "/f98",
            now = DateTime.UtcNow,
            artistId = 1,
            albumId = 2,
            genreId = 1
        });

        // Act
        new Migration17().Apply(fixture.Connection);

        // Assert
        Assert.Equal(1, fixture.Connection.QuerySingle<long?>("SELECT artistId FROM Tracks WHERE id = 98"));
        Assert.Equal(2, fixture.Connection.QuerySingle<long?>("SELECT albumId FROM Tracks WHERE id = 98"));
        Assert.Equal(1, fixture.Connection.QuerySingle<long?>("SELECT genreId FROM Tracks WHERE id = 98"));
    }
}