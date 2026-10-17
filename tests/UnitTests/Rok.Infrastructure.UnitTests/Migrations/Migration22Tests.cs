using Dapper;
using Rok.Domain.Enums;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration22Tests
{
    [Fact(DisplayName = "migration_22_adds_compilation_source_column")]
    public void Migration22_AddsCompilationSourceColumn()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        var column = fixture.Connection
            .Query<(string Name, string Type, int Required, string? Default)>("SELECT name, type, \"notnull\", dflt_value FROM pragma_table_info('albums') WHERE name = 'compilationSource'")
            .Single();

        // Assert
        Assert.Equal(("compilationSource", "INTEGER", 1, "0"), column);
    }

    [Fact(DisplayName = "migration_22_keeps_existing_albums_derived")]
    public void Migration22_ExistingAlbum_IsDerived()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        fixture.Connection.Execute("INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId) VALUES (9100, 'Legacy', 0, 1, 0, 1, 1, 0, 0, '2020-01-01', 1, 1)");

        // Act
        int source = fixture.Connection.ExecuteScalar<int>("SELECT compilationSource FROM Albums WHERE id = 9100");

        // Assert
        Assert.Equal((int)ECompilationSource.Derived, source);
    }

    [Fact(DisplayName = "migration_22_targets_version_22")]
    public void Migration22_TargetVersion_Is22()
    {
        // Act
        var version = new Migration22().TargetVersion;

        // Assert
        Assert.Equal(22, version);
    }
}