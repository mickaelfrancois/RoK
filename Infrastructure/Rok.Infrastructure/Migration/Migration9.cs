namespace Rok.Infrastructure.Migration;

public class Migration9 : IMigration
{
    public int TargetVersion => 9;

    public void Apply(IDbConnection connection)
    {
        string query = """
            CREATE TABLE ListeningEvents (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            trackId INTEGER NOT NULL,
            artistId INTEGER NULL,
            albumId INTEGER NULL,
            genreId INTEGER NULL,
            playedAt DATETIME NOT NULL,
            wasSkipped BOOLEAN NOT NULL,
            durationPlayed INTEGER NOT NULL,
            durationTotal INTEGER NOT NULL,

            FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE,
            FOREIGN KEY (ArtistId) REFERENCES Artists(Id) ON DELETE SET NULL,
            FOREIGN KEY (AlbumId) REFERENCES Albums(Id) ON DELETE SET NULL,
            FOREIGN KEY (GenreId) REFERENCES Genres(Id) ON DELETE SET NULL
            );
            """;

        connection.Execute(query);

        connection.Execute("CREATE INDEX idx_listeningEvents_playedAt ON ListeningEvents(PlayedAt);");
        connection.Execute("CREATE INDEX idx_listeningEvents_track ON ListeningEvents(TrackId);");
        connection.Execute("CREATE INDEX idx_listeningEvents_artist ON ListeningEvents(ArtistId);");
        connection.Execute("CREATE INDEX idx_listeningEvents_album ON ListeningEvents(AlbumId);");
        connection.Execute("CREATE INDEX idx_listeningEvents_genre ON ListeningEvents(GenreId);");
    }
}
