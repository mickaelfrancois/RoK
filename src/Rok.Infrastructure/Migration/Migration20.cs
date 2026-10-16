namespace Rok.Infrastructure.Migration;

/// <summary>
/// Adds <c>artists.renamePendingFrom</c>: the previous name of an artist whose rename is not fully written to the tags yet.
/// While it is set, the import treats the old name as an alias of the same artist.
/// </summary>
public class Migration20 : IMigration
{
    public int TargetVersion => 20;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE artists ADD COLUMN renamePendingFrom TEXT NULL;");
    }
}