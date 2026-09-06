namespace Rok.Infrastructure.Migration;

/// <summary>
/// Clears dangling artist, album and genre references carried by tracks. The Tracks table declares
/// no foreign key on those columns, so they could drift; ListeningEvents does enforce them, and an
/// insert built from a drifted track failed with "FOREIGN KEY constraint failed".
/// </summary>
public class Migration17 : IMigration
{
    public int TargetVersion => 17;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("UPDATE Tracks SET artistId = NULL WHERE artistId IS NOT NULL AND artistId NOT IN (SELECT id FROM Artists);");
        connection.Execute("UPDATE Tracks SET albumId = NULL WHERE albumId IS NOT NULL AND albumId NOT IN (SELECT id FROM Albums);");
        connection.Execute("UPDATE Tracks SET genreId = NULL WHERE genreId IS NOT NULL AND genreId NOT IN (SELECT id FROM Genres);");
    }
}