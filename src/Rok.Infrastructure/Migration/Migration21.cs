namespace Rok.Infrastructure.Migration;

/// <summary>
/// Creates <c>pendingTagWrites</c>: one row per track whose file does not carry yet the values the database holds.
/// <c>fields</c> is a <c>ETagWriteFields</c> bit mask. While a row exists, the import reads the database values instead of the file ones.
/// </summary>
public class Migration21 : IMigration
{
    public int TargetVersion => 21;

    public void Apply(IDbConnection connection)
    {
        string query = """
            CREATE TABLE pendingTagWrites (
            trackId INTEGER PRIMARY KEY NOT NULL REFERENCES Tracks(id) ON DELETE CASCADE,
            fields INTEGER NOT NULL
            );
            """;

        connection.Execute(query);
    }
}