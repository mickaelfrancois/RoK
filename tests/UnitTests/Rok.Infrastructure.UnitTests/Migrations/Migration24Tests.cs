using Dapper;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration24Tests
{
    [Fact(DisplayName = "migration_24_adds_the_has_compilation_flag_column")]
    public void Migration24_AddsHasCompilationFlagColumn()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        var column = fixture.Connection
            .Query<(string Name, string Type, int Required, string? Default)>("SELECT name, type, \"notnull\", dflt_value FROM pragma_table_info('tracks') WHERE name = 'hasCompilationFlag'")
            .Single();

        // Assert
        Assert.Equal(("hasCompilationFlag", "INTEGER", 1, "0"), column);
    }

    [Fact(DisplayName = "migration_24_flags_the_tracks_of_tag_compilations")]
    public void Migration24_Apply_FlagsOnlyTracksOfTagAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        fixture.Connection.Execute("ALTER TABLE tracks DROP COLUMN hasCompilationFlag");
        fixture.Connection.Execute("INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId, compilationSource) VALUES (9200, 'Tag', 0, 1, 0, 1, 1, 0, 0, '2020-01-01', 1, 1, 1), (9201, 'Derived', 0, 1, 0, 1, 1, 0, 0, '2020-01-01', 1, 1, 0), (9202, 'Manual', 0, 1, 0, 1, 1, 0, 0, '2020-01-01', 1, 1, 2)");
        fixture.Connection.Execute("INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, genreId) VALUES (9300, 'a', 1, 1, 1, '/m/9300.mp3', '2020-01-01', 0, 0, 0, 0, '2020-01-01', 9200, 1, 1), (9301, 'b', 1, 1, 1, '/m/9301.mp3', '2020-01-01', 0, 0, 0, 0, '2020-01-01', 9201, 1, 1), (9302, 'c', 1, 1, 1, '/m/9302.mp3', '2020-01-01', 0, 0, 0, 0, '2020-01-01', 9202, 1, 1)");

        // Act
        new Migration24().Apply(fixture.Connection);

        // Assert
        var flags = fixture.Connection
            .Query<(long Id, int Flag)>("SELECT id, hasCompilationFlag FROM Tracks WHERE id IN (9300, 9301, 9302) ORDER BY id")
            .ToList();

        Assert.Equal([(9300L, 1), (9301L, 0), (9302L, 0)], flags);
    }

    [Fact(DisplayName = "migration_24_targets_version_24")]
    public void Migration24_TargetVersion_Is24()
    {
        // Act
        var version = new Migration24().TargetVersion;

        // Assert
        Assert.Equal(24, version);
    }
}