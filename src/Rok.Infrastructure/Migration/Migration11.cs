namespace Rok.Infrastructure.Migration;

public class Migration11 : IMigration
{
    public int TargetVersion => 11;

    public void Apply(IDbConnection connection)
    {
        string createTable = """
            CREATE TABLE RadioStations (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                Name          TEXT    NOT NULL,
                StreamUrl     TEXT    NOT NULL,
                HomepageUrl   TEXT    NULL,
                AddedAt       TEXT    NOT NULL,
                LastListen    TEXT    NULL
            );
            """;

        string uniqueIndex = "CREATE UNIQUE INDEX UX_RadioStations_StreamUrl ON RadioStations(StreamUrl);";
        string lastListenIndex = "CREATE INDEX IX_RadioStations_LastListen ON RadioStations(LastListen DESC);";

        connection.Execute(createTable);
        connection.Execute(uniqueIndex);
        connection.Execute(lastListenIndex);
    }
}