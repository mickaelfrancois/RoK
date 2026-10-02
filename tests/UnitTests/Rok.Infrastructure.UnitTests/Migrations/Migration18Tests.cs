using Dapper;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration18Tests
{
    [Fact(DisplayName = "migration_18_creates_track_analysis_with_all_columns")]
    public void Migration18_CreatesTrackAnalysis_WithAllColumns()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        var columns = fixture.Connection
            .Query<string>("SELECT name FROM pragma_table_info('trackAnalysis')")
            .ToList();

        // Assert
        string[] expected =
        [
            "trackId", "algorithmVersion", "fileModifiedUtc", "fileSize",
            "musicEndSeconds", "fadeOutSeconds", "musicStartSeconds",
            "bpm", "bpmConfidence", "bpmSource",
            "introBeatPhase", "outroBeatPhase",
            "introTempoAnalysed", "outroTempoAnalysed"
        ];

        Assert.Subset(columns.ToHashSet(), expected.ToHashSet());
    }

    [Fact(DisplayName = "migration_18_targets_version_18")]
    public void Migration18_TargetVersion_Is18()
    {
        // Act
        var version = new Migration18().TargetVersion;

        // Assert
        Assert.Equal(18, version);
    }
}