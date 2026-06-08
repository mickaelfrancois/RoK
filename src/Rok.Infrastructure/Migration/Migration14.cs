namespace Rok.Infrastructure.Migration;

/// <summary>
/// Adds the extended audio-tag metadata read from TagLib during import.
/// Track-level columns (per file): disc number, BPM, composers, technical audio
/// characteristics (sample rate, bit depth, channels) and per-track ReplayGain.
/// Album-level columns (one value per release): disc count, per-album ReplayGain
/// and the MusicBrainz release type and country.
/// </summary>
public class Migration14 : IMigration
{
    public int TargetVersion => 14;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE Tracks ADD COLUMN disc INTEGER NULL;");
        connection.Execute("ALTER TABLE Tracks ADD COLUMN bpm INTEGER NULL;");
        connection.Execute("ALTER TABLE Tracks ADD COLUMN composers TEXT NULL;");
        connection.Execute("ALTER TABLE Tracks ADD COLUMN sampleRate INTEGER NOT NULL DEFAULT 0;");
        connection.Execute("ALTER TABLE Tracks ADD COLUMN bitsPerSample INTEGER NOT NULL DEFAULT 0;");
        connection.Execute("ALTER TABLE Tracks ADD COLUMN channels INTEGER NOT NULL DEFAULT 0;");
        connection.Execute("ALTER TABLE Tracks ADD COLUMN replayGainTrackGain REAL NULL;");
        connection.Execute("ALTER TABLE Tracks ADD COLUMN replayGainTrackPeak REAL NULL;");

        connection.Execute("ALTER TABLE Albums ADD COLUMN discCount INTEGER NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN replayGainAlbumGain REAL NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN replayGainAlbumPeak REAL NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN musicBrainzReleaseType TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN musicBrainzReleaseCountry TEXT NULL;");
    }
}
