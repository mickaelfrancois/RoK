namespace Rok.Infrastructure.Migration;

public class Migration2 : IMigration
{
    public int TargetVersion => 2;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE Tracks ADD COLUMN getLyricsLastAttempt TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN getMetaDataLastAttempt TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN getMetaDataLastAttempt TEXT NULL;");

        InitializeTracksFields(connection);
        InitializeArtistsFields(connection);
        InitializeAlbumsFields(connection);
    }

    private static void InitializeTracksFields(IDbConnection connection)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE Tracks SET getLyricsLastAttempt = @utc WHERE getLyricsLastAttempt IS NULL;";

        IDbDataParameter param = command.CreateParameter();
        param.ParameterName = "@utc";
        param.DbType = DbType.DateTime;
        param.Value = DateTime.UtcNow;

        command.Parameters.Add(param);
        command.ExecuteNonQuery();
    }

    private static void InitializeArtistsFields(IDbConnection connection)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE Albums SET getMetaDataLastAttempt = @utc WHERE getMetaDataLastAttempt IS NULL;";

        IDbDataParameter param = command.CreateParameter();
        param.ParameterName = "@utc";
        param.DbType = DbType.DateTime;
        param.Value = DateTime.UtcNow;

        command.Parameters.Add(param);
        command.ExecuteNonQuery();
    }

    private static void InitializeAlbumsFields(IDbConnection connection)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE Artists SET getMetaDataLastAttempt = @utc WHERE getMetaDataLastAttempt IS NULL;";

        IDbDataParameter param = command.CreateParameter();
        param.ParameterName = "@utc";
        param.DbType = DbType.DateTime;
        param.Value = DateTime.UtcNow;

        command.Parameters.Add(param);
        command.ExecuteNonQuery();
    }
}
