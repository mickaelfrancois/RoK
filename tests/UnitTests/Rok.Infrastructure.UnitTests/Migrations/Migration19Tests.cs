using Dapper;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration19Tests
{
    [Fact(DisplayName = "migration_19_adds_the_downbeat_and_mix_point_columns")]
    public void Migration19_AddsNullableRealColumns()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        var columns = fixture.Connection
            .Query<(string Name, string Type, int Required)>("SELECT name, type, \"notnull\" FROM pragma_table_info('trackAnalysis')")
            .ToDictionary(c => c.Name);

        // Assert
        foreach (var name in new[] { "introDownbeatSeconds", "outroDownbeatSeconds", "outroMixPointSeconds", "outroMixPointScore" })
        {
            Assert.True(columns.ContainsKey(name), name);
            Assert.Equal("REAL", columns[name].Type);
            Assert.Equal(0, columns[name].Required);
        }
    }

    [Fact(DisplayName = "migration_19_leaves_existing_rows_with_null_values")]
    public void Migration19_ExistingRows_HaveNullValues()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        fixture.Connection.Execute("INSERT INTO trackAnalysis(trackId, algorithmVersion, fileModifiedUtc, fileSize) VALUES (1, 1, @now, 10)", new { now = DateTime.UtcNow });

        // Act
        var row = fixture.Connection.QuerySingle<(double? Downbeat, double? Score)>("SELECT outroDownbeatSeconds AS Downbeat, outroMixPointScore AS Score FROM trackAnalysis WHERE trackId = 1");

        // Assert
        Assert.Null(row.Downbeat);
        Assert.Null(row.Score);
    }

    [Fact(DisplayName = "migration_19_targets_version_19")]
    public void Migration19_TargetVersion_Is19()
    {
        // Act
        var version = new Migration19().TargetVersion;

        // Assert
        Assert.Equal(19, version);
    }
}