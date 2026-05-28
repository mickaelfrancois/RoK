using Dapper;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration11Tests
{
    [Fact(DisplayName = "Migration 11 should create RadioStations table with expected columns")]
    public void Migration11_ShouldCreateRadioStationsTable_WithExpectedColumns()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act — fixture applies all migrations including Migration11

        // Assert — table exists and has the expected columns
        string[] columns = fixture.Connection
            .Query<string>("SELECT name FROM pragma_table_info('RadioStations')")
            .ToArray();

        Assert.Contains("Id", columns);
        Assert.Contains("Name", columns);
        Assert.Contains("StreamUrl", columns);
        Assert.Contains("HomepageUrl", columns);
        Assert.Contains("AddedAt", columns);
        Assert.Contains("LastListen", columns);
    }

    [Fact(DisplayName = "Migration 11 should create a unique index on StreamUrl")]
    public void Migration11_ShouldCreateUniqueIndex_OnStreamUrl()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act / Assert
        string? indexName = fixture.Connection.QueryFirstOrDefault<string>(
            "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'RadioStations' AND sql LIKE '%UNIQUE%StreamUrl%'");

        Assert.Equal("UX_RadioStations_StreamUrl", indexName);
    }
}
