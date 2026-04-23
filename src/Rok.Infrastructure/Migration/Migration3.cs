namespace Rok.Infrastructure.Migration;

public class Migration3 : IMigration
{
    public int TargetVersion => 3;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE Artists ADD COLUMN flickrUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN instagramUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN tiktokUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN threadsUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN songkickUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN soundcloundUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN imdbUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN lastfmUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN discogsUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN bandsintownUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN youtubeUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN audioDbID TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN allMusicUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Artists ADD COLUMN isLock INTEGER NOT NULL DEFAULT 0;");

        connection.Execute("ALTER TABLE Albums ADD COLUMN biography TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN lastFmUrl TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN releaseGroupMusicBrainzID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN AudioDbID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN AudioDbArtistID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN AllMusicID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN DiscogsID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN MusicMozID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN LyricWikiID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN GeniusID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN WikipediaID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN WikidataID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN AmazonID TEXT NULL;");
        connection.Execute("ALTER TABLE Albums ADD COLUMN isLock INTEGER NOT NULL DEFAULT 0;");

        connection.Execute("UPDATE Albums SET getMetaDataLastAttempt = NULL WHERE getMetaDataLastAttempt IS NOT NULL;");
        connection.Execute("UPDATE Artists SET getMetaDataLastAttempt = NULL WHERE getMetaDataLastAttempt IS NOT NULL;");
    }
}
