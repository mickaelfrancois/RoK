namespace Rok.Infrastructure.Migration;

/// <summary>
/// Adds <c>tracks.hasCompilationFlag</c>, a mirror of the compilation flag (TCMP / COMPILATION / cpil) carried by the file.
/// It only lets the rescan detect a flag change on its own. Tracks of albums whose status comes from the tag start at 1,
/// the others at 0: a track whose file disagrees is processed again once, with no visible effect.
/// </summary>
public class Migration24 : IMigration
{
    public int TargetVersion => 24;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE tracks ADD COLUMN hasCompilationFlag INTEGER NOT NULL DEFAULT 0;");

        connection.Execute("UPDATE tracks SET hasCompilationFlag = 1 WHERE albumId IN (SELECT id FROM albums WHERE compilationSource = 1);");
    }
}