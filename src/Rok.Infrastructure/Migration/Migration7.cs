namespace Rok.Infrastructure.Migration;

public class Migration7 : IMigration
{
    public int TargetVersion => 7;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("UPDATE artists SET totalDurationSeconds = (SELECT COALESCE(SUM(duration), 0) FROM tracks WHERE tracks.artistId = artists.id);");
        connection.Execute("UPDATE genres SET totalDurationSeconds = (SELECT COALESCE(SUM(duration), 0) FROM tracks WHERE tracks.genreId = genres.id);");
        connection.Execute("UPDATE albums SET duration = (SELECT COALESCE(SUM(duration), 0) FROM tracks WHERE tracks.albumId = albums.id);");
    }
}