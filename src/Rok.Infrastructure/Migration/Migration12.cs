namespace Rok.Infrastructure.Migration;

public class Migration12 : IMigration
{
    public int TargetVersion => 12;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN StationUuid TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN FaviconUrl  TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN CountryCode TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN Codec       TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN Bitrate     INTEGER NULL;");
    }
}
