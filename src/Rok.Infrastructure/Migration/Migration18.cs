namespace Rok.Infrastructure.Migration;

/// <summary>
/// Creates <c>trackAnalysis</c>, the per-track cache of the Mix analysis (cues, tempo, beat phase).
/// <c>bpmSource</c>: 1 = tag, 2 = detected. The row is filled window by window, hence the per-window tempo flags.
/// </summary>
public class Migration18 : IMigration
{
    public int TargetVersion => 18;

    public void Apply(IDbConnection connection)
    {
        string query = """
            CREATE TABLE trackAnalysis (
            trackId INTEGER PRIMARY KEY NOT NULL REFERENCES Tracks(id) ON DELETE CASCADE,
            algorithmVersion INTEGER NOT NULL,
            fileModifiedUtc DATETIME NOT NULL,
            fileSize INTEGER NOT NULL,
            musicEndSeconds REAL NULL,
            fadeOutSeconds REAL NULL,
            musicStartSeconds REAL NULL,
            bpm REAL NULL,
            bpmConfidence REAL NULL,
            bpmSource INTEGER NULL,
            introBeatPhase REAL NULL,
            outroBeatPhase REAL NULL,
            introTempoAnalysed INTEGER NOT NULL DEFAULT 0,
            outroTempoAnalysed INTEGER NOT NULL DEFAULT 0
            );
            """;

        connection.Execute(query);
    }
}