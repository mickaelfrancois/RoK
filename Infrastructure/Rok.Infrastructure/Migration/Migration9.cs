namespace Rok.Infrastructure.Migration;

public class Migration9 : IMigration
{
    public int TargetVersion => 9;

    public void Apply(IDbConnection connection)
    {
        string query = """
            CREATE TABLE ListeningEvent (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            TrackId INTEGER NOT NULL,
            ArtistId INTEGER NULL,
            AlbumId INTEGER NULL,
            GenreId INTEGER NULL,
            PlayedAt DATETIME NOT NULL,
            WasSkipped BOOLEAN NOT NULL,

            INDEX idx_playedAt (PlayedAt),
            INDEX idx_track (TrackId),
            INDEX idx_artist (ArtistId),
            INDEX idx_album (AlbumId),
            INDEX idx_genre (GenreId),

            FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE,
            FOREIGN KEY (ArtistId) REFERENCES Artists(Id) ON DELETE SET NULL,
            FOREIGN KEY (AlbumId) REFERENCES Albums(Id) ON DELETE SET NULL,
            FOREIGN KEY (GenreId) REFERENCES Genres(Id) ON DELETE SET NULL
            );
            """;

        connection.Execute(query);
    }
}
