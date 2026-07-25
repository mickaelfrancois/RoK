namespace Rok.Infrastructure.Migration;

/// <summary>
/// Adds the ShuffleOnPlay flag to Playlists, letting a playlist opt into shuffled
/// playback whenever it starts listening.
/// </summary>
public class Migration15 : IMigration
{
    public int TargetVersion => 15;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE Playlists ADD COLUMN shuffleOnPlay INTEGER NOT NULL DEFAULT 0;");
    }
}