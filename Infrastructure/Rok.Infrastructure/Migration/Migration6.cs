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

/*
 * select t.title
from tracks as t
where t.id in (
 
 SELECT trackId 
     FROM track_tags 
     JOIN tags ON tags.id = track_tags.tagId
     WHERE tags.name IN ('soirée')
     GROUP BY trackId
     HAVING COUNT(DISTINCT tags.id) > 0
 );
 
SELECT trackId 
     FROM track_tags 
     JOIN tags ON tags.id = track_tags.tagId
     WHERE tags.name IN ('soirée')
     GROUP BY trackId
     HAVING COUNT(DISTINCT tags.id) > 0;
     


 SELECT tracks.title, 
        GROUP_CONCAT(tags.Name) AS TagsAsString
 FROM tracks
 LEFT JOIN Track_Tags ON tracks.Id = Track_Tags.TrackId
 LEFT JOIN Tags ON Track_Tags.TagId = Tags.Id
 where tags.name ='violent'
 GROUP BY tracks.Id
 */