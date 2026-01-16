namespace Rok.Infrastructure.Migration;

public class Migration5 : IMigration
{
    public int TargetVersion => 5;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("UPDATE albums SET trackCount = (SELECT COUNT(*) FROM tracks WHERE tracks.albumId = albums.id)");
        connection.Execute("UPDATE artists SET trackCount = (SELECT COUNT(*) FROM tracks WHERE tracks.artistId = artists.id)");
        connection.Execute("UPDATE genres SET trackCount = (SELECT COUNT(*) FROM tracks WHERE tracks.genreId = genres.id)");
    }
}
