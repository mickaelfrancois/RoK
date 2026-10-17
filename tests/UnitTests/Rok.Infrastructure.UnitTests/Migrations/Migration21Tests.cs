using Dapper;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration21Tests
{
    [Fact(DisplayName = "migration_21_creates_pending_tag_writes")]
    public void Migration21_CreatesPendingTagWritesTable()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        var columns = fixture.Connection
            .Query<(string Name, string Type, int Required, int PrimaryKey)>("SELECT name, type, \"notnull\", pk FROM pragma_table_info('pendingTagWrites') ORDER BY cid")
            .ToList();

        // Assert
        Assert.Equal(2, columns.Count);
        Assert.Equal(("trackId", "INTEGER", 1, 1), columns[0]);
        Assert.Equal(("fields", "INTEGER", 1, 0), columns[1]);
    }

    [Fact(DisplayName = "migration_21_starts_with_an_empty_table")]
    public void Migration21_NewTable_IsEmpty()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        int rows = fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pendingTagWrites");

        // Assert
        Assert.Equal(0, rows);
    }

    [Fact(DisplayName = "migration_21_targets_version_21")]
    public void Migration21_TargetVersion_Is21()
    {
        // Act
        var version = new Migration21().TargetVersion;

        // Assert
        Assert.Equal(21, version);
    }
}