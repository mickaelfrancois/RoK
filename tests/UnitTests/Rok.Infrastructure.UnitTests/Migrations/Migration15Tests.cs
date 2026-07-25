using Dapper;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration15Tests
{
    [Fact(DisplayName = "Migration 15 should add shuffleOnPlay column to Playlists")]
    public void Migration15_ShouldAddShuffleOnPlayColumn_ToPlaylists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act — fixture applies all migrations including Migration15

        // Assert — column exists on Playlists
        string[] columns = fixture.Connection
            .Query<string>("SELECT name FROM pragma_table_info('Playlists')")
            .ToArray();

        Assert.Contains("shuffleOnPlay", columns);
    }

    [Fact(DisplayName = "Migration 15 should default shuffleOnPlay to zero")]
    public void Migration15_ShouldDefaultShuffleOnPlay_ToZero()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        string? defaultValue = fixture.Connection.QueryFirstOrDefault<string>(
            "SELECT dflt_value FROM pragma_table_info('Playlists') WHERE name = 'shuffleOnPlay'");

        // Assert
        Assert.Equal("0", defaultValue);
    }
}