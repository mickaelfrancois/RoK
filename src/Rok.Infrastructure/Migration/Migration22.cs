namespace Rok.Infrastructure.Migration;

/// <summary>
/// Adds <c>albums.compilationSource</c>, the origin of the compilation status (0 = derived, 1 = tag, 2 = manual).
/// Existing albums start as derived: the next rescan promotes to tag the ones whose files carry the flag.
/// </summary>
public class Migration22 : IMigration
{
    public int TargetVersion => 22;

    public void Apply(IDbConnection connection)
    {
        string query = "ALTER TABLE albums ADD COLUMN compilationSource INTEGER NOT NULL DEFAULT 0;";

        connection.Execute(query);
    }
}