namespace Rok.Infrastructure.Migration;

/// <summary>
/// Index tuning based on EXPLAIN QUERY PLAN measurements against a real library (42k tracks):
/// adds a case-insensitive index on tracks.musicFile (the COLLATE NOCASE lookup in
/// GetByFilePathAsync cannot use the BINARY unique index and was doing a full scan),
/// indexes the tagId side of the tag link tables (composite PK only covers the entity side),
/// and drops low-selectivity indexes that were never picked by the planner or made
/// queries slower than a sequential scan while taxing every write.
/// </summary>
public class Migration13 : IMigration
{
    public int TargetVersion => 13;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("CREATE INDEX IF NOT EXISTS Idx_Tracks_musicFile_nocase ON Tracks (musicFile COLLATE NOCASE);");

        connection.Execute("CREATE INDEX IF NOT EXISTS Idx_TrackTags_tagId ON TrackTags (tagId);");
        connection.Execute("CREATE INDEX IF NOT EXISTS Idx_AlbumTags_tagId ON AlbumTags (tagId);");
        connection.Execute("CREATE INDEX IF NOT EXISTS Idx_ArtistTags_tagId ON ArtistTags (tagId);");

        connection.Execute("DROP INDEX IF EXISTS Idx_Tracks_isLive;");
        connection.Execute("DROP INDEX IF EXISTS Idx_Tracks_skipCount;");
        connection.Execute("DROP INDEX IF EXISTS Idx_Tracks_lastSkip;");
        connection.Execute("DROP INDEX IF EXISTS Idx_Tracks_size;");
        connection.Execute("DROP INDEX IF EXISTS Idx_Tracks_bitrate;");

        connection.Execute("DROP INDEX IF EXISTS Idx_Albums_isFavorite;");
        connection.Execute("DROP INDEX IF EXISTS Idx_Albums_isLive;");
        connection.Execute("DROP INDEX IF EXISTS Idx_Albums_isCompilation;");
        connection.Execute("DROP INDEX IF EXISTS Idx_Albums_isBestof;");

        connection.Execute("DROP INDEX IF EXISTS Idx_Artists_isFavorite;");

        connection.Execute("DROP INDEX IF EXISTS Idx_Genres_isFavorite;");
    }
}
