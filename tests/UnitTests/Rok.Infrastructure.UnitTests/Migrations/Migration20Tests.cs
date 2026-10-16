using Dapper;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration20Tests
{
    [Fact(DisplayName = "migration_20_adds_the_nullable_rename_pending_from_column")]
    public void Migration20_AddsNullableTextColumn()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        var column = fixture.Connection
            .Query<(string Name, string Type, int Required)>("SELECT name, type, \"notnull\" FROM pragma_table_info('artists') WHERE name = 'renamePendingFrom'")
            .SingleOrDefault();

        // Assert
        Assert.Equal("renamePendingFrom", column.Name);
        Assert.Equal("TEXT", column.Type);
        Assert.Equal(0, column.Required);
    }

    [Fact(DisplayName = "migration_20_leaves_existing_artists_without_a_pending_rename")]
    public void Migration20_ExistingArtists_HaveNullMarker()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        int withMarker = fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM artists WHERE renamePendingFrom IS NOT NULL");

        // Assert
        Assert.Equal(0, withMarker);
    }

    [Fact(DisplayName = "migration_20_targets_version_20")]
    public void Migration20_TargetVersion_Is20()
    {
        // Act
        var version = new Migration20().TargetVersion;

        // Assert
        Assert.Equal(20, version);
    }
}