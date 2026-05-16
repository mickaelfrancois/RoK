namespace Rok.Infrastructure.Migration;

public class Migration10 : IMigration
{
    public int TargetVersion => 10;

    public void Apply(IDbConnection connection)
    {
        string query = """
            CREATE TABLE EqualizerPreset (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Scope     TEXT    NOT NULL,
                ScopeId   INTEGER,           
                Band0     REAL    NOT NULL DEFAULT 0,  -- 32Hz
                Band1     REAL    NOT NULL DEFAULT 0,  -- 64Hz
                Band2     REAL    NOT NULL DEFAULT 0,  -- 125Hz
                Band3     REAL    NOT NULL DEFAULT 0,  -- 250Hz
                Band4     REAL    NOT NULL DEFAULT 0,  -- 500Hz
                Band5     REAL    NOT NULL DEFAULT 0,  -- 1kHz
                Band6     REAL    NOT NULL DEFAULT 0,  -- 2kHz
                Band7     REAL    NOT NULL DEFAULT 0,  -- 4kHz
                Band8     REAL    NOT NULL DEFAULT 0,  -- 8kHz
                Band9     REAL    NOT NULL DEFAULT 0,  -- 16kHz
                UNIQUE(Scope, ScopeId)
            );            
            """;

        connection.Execute(query);
    }
}