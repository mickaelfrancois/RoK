using Dapper;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration16Tests
{
    [Fact(DisplayName = "Migration 16 should add refreshOnPlay column to Playlists")]
    public void Migration16_ShouldAddRefreshOnPlayColumn_ToPlaylists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act — fixture applies all migrations including Migration16

        // Assert — column exists on Playlists
        string[] columns = fixture.Connection
            .Query<string>("SELECT name FROM pragma_table_info('Playlists')")
            .ToArray();

        Assert.Contains("refreshOnPlay", columns);
    }

    [Fact(DisplayName = "Migration 16 should default refreshOnPlay to zero")]
    public void Migration16_ShouldDefaultRefreshOnPlay_ToZero()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        string? defaultValue = fixture.Connection.QueryFirstOrDefault<string>(
            "SELECT dflt_value FROM pragma_table_info('Playlists') WHERE name = 'refreshOnPlay'");

        // Assert
        Assert.Equal("0", defaultValue);
    }
}