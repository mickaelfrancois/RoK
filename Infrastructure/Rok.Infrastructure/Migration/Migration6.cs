namespace Rok.Infrastructure.Migration;

public class Migration6 : IMigration
{
    public int TargetVersion => 6;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("CREATE TABLE Tags (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE);");
        connection.Execute("CREATE TABLE AlbumTags (albumId INTEGER NOT NULL, tagId INTEGER NOT NULL, PRIMARY KEY (albumId, tagId), FOREIGN KEY (albumId) REFERENCES albums(Id) ON DELETE CASCADE, FOREIGN KEY (tagId) REFERENCES tags(id) ON DELETE CASCADE);");
        connection.Execute("CREATE TABLE ArtistTags (artistId INTEGER NOT NULL, tagId INTEGER NOT NULL, PRIMARY KEY (artistId, tagId), FOREIGN KEY (artistId) REFERENCES artists(Id) ON DELETE CASCADE, FOREIGN KEY (tagId) REFERENCES tags(id) ON DELETE CASCADE);");
        connection.Execute("CREATE TABLE TrackTags (trackId INTEGER NOT NULL, tagId INTEGER NOT NULL, PRIMARY KEY (trackId, tagId), FOREIGN KEY (trackId) REFERENCES tracks(id) ON DELETE CASCADE, FOREIGN KEY (tagId) REFERENCES tags(id) ON DELETE CASCADE);");
    }
}
