using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories.Sql;

namespace Rok.Infrastructure.Migration;

/// <summary>
/// Makes artist and genre names unique regardless of case. Case-only duplicates are merged first (oldest entity survives,
/// user data is combined, references are moved), then the unique indexes are recreated with <c>COLLATE NOCASE</c>.
/// Albums sharing a name under the surviving artist are left as they are.
/// </summary>
public class Migration23 : IMigration
{
    private const string SelectArtistsSql = "SELECT id, name FROM artists ORDER BY creatDate, id";
    private const string SelectGenresSql = "SELECT id, name FROM genres ORDER BY creatDate, id";

    private static readonly string[] ArtistFillColumns =
    [
        "wikipediaUrl", "officialSiteUrl", "facebookUrl", "twitterUrl", "novaUid", "musicBrainzID", "flickrUrl", "instagramUrl",
        "tiktokUrl", "threadsUrl", "songkickUrl", "soundcloundUrl", "imdbUrl", "lastfmUrl", "discogsUrl", "bandsintownUrl",
        "youtubeUrl", "audioDbID", "allMusicUrl", "formedYear", "bornYear", "diedYear", "style", "gender", "mood", "members",
        "similarArtists", "biography", "countryId", "genreId", "pictureDominantColor"
    ];

    public int TargetVersion => 23;

    public void Apply(IDbConnection connection)
    {
        if (connection.State == ConnectionState.Closed)
            connection.Open();

        using IDbTransaction transaction = connection.BeginTransaction();

        try
        {
            HashSet<long> touchedGenres = [];
            List<long> survivorArtists = MergeArtists(connection, transaction, touchedGenres);
            List<long> survivorGenres = MergeGenres(connection, transaction);

            RecomputeCounters(connection, transaction, survivorArtists, survivorGenres, touchedGenres);
            RecreateIndexes(connection, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }

    private static List<long> MergeArtists(IDbConnection connection, IDbTransaction transaction, HashSet<long> touchedGenres)
    {
        List<long> survivors = [];

        foreach ((long survivorId, List<long> sourceIds) in FindGroups(connection, transaction, SelectArtistsSql))
        {
            survivors.Add(survivorId);

            foreach (long sourceId in sourceIds)
            {
                long? sourceGenre = connection.ExecuteScalar<long?>("SELECT genreId FROM artists WHERE id = @sourceId", new { sourceId }, transaction);

                if (sourceGenre is not null)
                    touchedGenres.Add(sourceGenre.Value);

                MergeArtist(connection, transaction, survivorId, sourceId);
            }

            long? survivorGenre = connection.ExecuteScalar<long?>("SELECT genreId FROM artists WHERE id = @survivorId", new { survivorId }, transaction);

            if (survivorGenre is not null)
                touchedGenres.Add(survivorGenre.Value);
        }

        return survivors;
    }

    private static void MergeArtist(IDbConnection connection, IDbTransaction transaction, long targetId, long sourceId)
    {
        object ids = new { targetId, sourceId };

        string fill = string.Join(", ", ArtistFillColumns.Select(c => $"{c} = COALESCE(NULLIF({c}, ''), (SELECT {c} FROM artists WHERE id = @sourceId))"));

        connection.Execute($"""
            UPDATE artists SET
                isFavorite = MAX(isFavorite, (SELECT isFavorite FROM artists WHERE id = @sourceId)),
                isLock = MAX(isLock, (SELECT isLock FROM artists WHERE id = @sourceId)),
                listenCount = listenCount + (SELECT listenCount FROM artists WHERE id = @sourceId),
                lastListen = (SELECT MAX(lastListen) FROM artists WHERE id IN (@targetId, @sourceId)),
                {fill}
            WHERE id = @targetId
            """, ids, transaction);

        connection.Execute("UPDATE tracks SET artistId = @targetId WHERE artistId = @sourceId", ids, transaction);
        connection.Execute("UPDATE albums SET artistId = @targetId WHERE artistId = @sourceId", ids, transaction);
        connection.Execute("UPDATE ListeningEvents SET artistId = @targetId WHERE artistId = @sourceId", ids, transaction);
        connection.Execute("INSERT OR IGNORE INTO ArtistTags(artistId, tagId) SELECT @targetId, tagId FROM ArtistTags WHERE artistId = @sourceId", ids, transaction);
        connection.Execute("DELETE FROM ArtistTags WHERE artistId = @sourceId", ids, transaction);

        MovePreset(connection, transaction, nameof(EqualizerScope.Artist), targetId, sourceId);

        connection.Execute("DELETE FROM artists WHERE id = @sourceId", ids, transaction);
    }

    private static List<long> MergeGenres(IDbConnection connection, IDbTransaction transaction)
    {
        List<long> survivors = [];

        foreach ((long survivorId, List<long> sourceIds) in FindGroups(connection, transaction, SelectGenresSql))
        {
            survivors.Add(survivorId);

            foreach (long sourceId in sourceIds)
            {
                MergeGenre(connection, transaction, survivorId, sourceId);
            }
        }

        return survivors;
    }

    private static void MergeGenre(IDbConnection connection, IDbTransaction transaction, long targetId, long sourceId)
    {
        object ids = new { targetId, sourceId };

        connection.Execute("""
            UPDATE genres SET
                isFavorite = MAX(isFavorite, (SELECT isFavorite FROM genres WHERE id = @sourceId)),
                listenCount = listenCount + (SELECT listenCount FROM genres WHERE id = @sourceId),
                lastListen = (SELECT MAX(lastListen) FROM genres WHERE id IN (@targetId, @sourceId))
            WHERE id = @targetId
            """, ids, transaction);

        connection.Execute("UPDATE tracks SET genreId = @targetId WHERE genreId = @sourceId", ids, transaction);
        connection.Execute("UPDATE albums SET genreId = @targetId WHERE genreId = @sourceId", ids, transaction);
        connection.Execute("UPDATE artists SET genreId = @targetId WHERE genreId = @sourceId", ids, transaction);
        connection.Execute("UPDATE ListeningEvents SET genreId = @targetId WHERE genreId = @sourceId", ids, transaction);

        MovePreset(connection, transaction, nameof(EqualizerScope.Genre), targetId, sourceId);

        connection.Execute("DELETE FROM genres WHERE id = @sourceId", ids, transaction);
    }

    private static void MovePreset(IDbConnection connection, IDbTransaction transaction, string scope, long targetId, long sourceId)
    {
        bool targetHasPreset = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = @scope AND ScopeId = @targetId", new { scope, targetId }, transaction) > 0;

        if (targetHasPreset)
        {
            connection.Execute("DELETE FROM EqualizerPreset WHERE Scope = @scope AND ScopeId = @sourceId", new { scope, sourceId }, transaction);

            return;
        }

        connection.Execute("UPDATE EqualizerPreset SET ScopeId = @targetId WHERE Scope = @scope AND ScopeId = @sourceId", new { scope, targetId, sourceId }, transaction);
    }

    private static List<(long SurvivorId, List<long> SourceIds)> FindGroups(IDbConnection connection, IDbTransaction transaction, string sql)
    {
        Dictionary<string, (long SurvivorId, List<long> SourceIds)> groups = new(StringComparer.Ordinal);

        foreach ((long id, string name) in connection.Query<(long Id, string Name)>(sql, transaction: transaction).ToList())
        {
            string key = name.ToUpperInvariant();

            if (groups.TryGetValue(key, out (long SurvivorId, List<long> SourceIds) group))
            {
                group.SourceIds.Add(id);

                continue;
            }

            groups[key] = (id, []);
        }

        return [.. groups.Values.Where(g => g.SourceIds.Count > 0)];
    }

    private static void RecomputeCounters(IDbConnection connection, IDbTransaction transaction, List<long> artistIds, List<long> genreIds, HashSet<long> touchedGenres)
    {
        foreach (long artistId in artistIds)
        {
            connection.Execute(LibraryCounterSql.UpdateArtistSql, new { artistId }, transaction);

            foreach (long albumId in connection.Query<long>("SELECT id FROM albums WHERE artistId = @artistId", new { artistId }, transaction).ToList())
            {
                connection.Execute(LibraryCounterSql.UpdateAlbumSql, new { albumId }, transaction);
            }
        }

        foreach (long genreId in genreIds.Concat(touchedGenres).Distinct())
        {
            connection.Execute(LibraryCounterSql.UpdateGenreTrackCountersSql, new { genreId }, transaction);
            connection.Execute(LibraryCounterSql.UpdateGenreArtistCountSql, new { genreId }, transaction);
            connection.Execute(LibraryCounterSql.UpdateGenreAlbumCountersSql, new { genreId }, transaction);
        }
    }

    private static void RecreateIndexes(IDbConnection connection, IDbTransaction transaction)
    {
        connection.Execute("DROP INDEX IF EXISTS Artists_name", transaction: transaction);
        connection.Execute("CREATE UNIQUE INDEX Artists_name ON Artists(name COLLATE NOCASE)", transaction: transaction);
        connection.Execute("DROP INDEX IF EXISTS Genres_name", transaction: transaction);
        connection.Execute("CREATE UNIQUE INDEX Genres_name ON Genres(name COLLATE NOCASE)", transaction: transaction);
    }
}