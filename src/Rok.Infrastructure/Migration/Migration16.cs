namespace Rok.Infrastructure.Migration;

/// <summary>
/// Adds the RefreshOnPlay flag to Playlists, letting a smart playlist regenerate
/// its selection right before playback starts.
/// </summary>
public class Migration16 : IMigration
{
    public int TargetVersion => 16;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE Playlists ADD COLUMN refreshOnPlay INTEGER NOT NULL DEFAULT 0;");
    }
}