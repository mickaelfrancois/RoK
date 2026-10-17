namespace Rok.Infrastructure.Repositories.Sql;

/// <summary>
/// SQL that recomputes the counters of artists, albums and genres inside an open transaction of the shared connection.
/// The rules are the ones of <c>Statistics</c> in the import.
/// </summary>
internal static class LibraryCounterSql
{
    private const string UpdateAlbumSql = "UPDATE albums SET trackCount = (SELECT COUNT(*) FROM tracks WHERE albumId = @albumId), duration = (SELECT COALESCE(SUM(duration), 0) FROM tracks WHERE albumId = @albumId) WHERE id = @albumId";

    private const string UpdateArtistSql = """
        UPDATE artists SET
            trackCount = (SELECT COUNT(*) FROM tracks WHERE artistId = @artistId),
            totalDurationSeconds = (SELECT COALESCE(SUM(duration), 0) FROM tracks WHERE artistId = @artistId),
            albumCount = (SELECT COUNT(*) FROM albums WHERE artistId = @artistId AND isCompilation = 0 AND isLive = 0 AND isBestof = 0),
            bestofCount = (SELECT COUNT(*) FROM albums WHERE artistId = @artistId AND isBestof = 1 AND isCompilation = 0),
            liveCount = (SELECT COUNT(*) FROM albums WHERE artistId = @artistId AND isLive = 1 AND isCompilation = 0),
            compilationCount = (SELECT COUNT(*) FROM albums WHERE artistId = @artistId AND isCompilation = 1),
            yearMini = NULLIF((SELECT MIN(year) FROM albums WHERE artistId = @artistId AND year IS NOT NULL AND isCompilation = 0 AND isLive = 0 AND isBestof = 0), 0),
            yearMaxi = NULLIF((SELECT MAX(year) FROM albums WHERE artistId = @artistId AND year IS NOT NULL AND isCompilation = 0 AND isLive = 0 AND isBestof = 0), 0)
        WHERE id = @artistId
        """;

    private const string UpdateGenreArtistCountSql = "UPDATE genres SET artistCount = (SELECT COUNT(*) FROM artists WHERE genreId = @genreId AND EXISTS (SELECT 1 FROM tracks WHERE tracks.artistId = artists.id)) WHERE id = @genreId";

    private const string SelectGenresOfArtistsSql = "SELECT DISTINCT genreId FROM artists WHERE id IN @artistIds AND genreId IS NOT NULL";

    private const string UpdateGenreAlbumCountersSql = """
        UPDATE genres SET
            albumCount = (SELECT COUNT(*) FROM albums WHERE genreId = @genreId AND isCompilation = 0 AND isLive = 0 AND isBestof = 0),
            bestofCount = (SELECT COUNT(*) FROM albums WHERE genreId = @genreId AND isBestof = 1 AND isCompilation = 0),
            liveCount = (SELECT COUNT(*) FROM albums WHERE genreId = @genreId AND isLive = 1 AND isCompilation = 0),
            compilationCount = (SELECT COUNT(*) FROM albums WHERE genreId = @genreId AND isCompilation = 1)
        WHERE id = @genreId
        """;

    private const string UpdateGenreTrackCountersSql = """
        UPDATE genres SET
            trackCount = (SELECT COUNT(*) FROM tracks WHERE genreId = @genreId),
            totalDurationSeconds = (SELECT COALESCE(SUM(duration), 0) FROM tracks WHERE genreId = @genreId)
        WHERE id = @genreId
        """;

    /// <summary>Recomputes track count and duration of an album.</summary>
    public static Task UpdateAlbumAsync(IDbConnection connection, long albumId, IDbTransaction transaction, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(UpdateAlbumSql, new { albumId }, transaction, cancellationToken: cancellationToken));

    /// <summary>Recomputes every counter and the year range of an artist.</summary>
    public static Task UpdateArtistAsync(IDbConnection connection, long artistId, IDbTransaction transaction, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(UpdateArtistSql, new { artistId }, transaction, cancellationToken: cancellationToken));

    /// <summary>Recomputes the artist count of a genre.</summary>
    public static Task UpdateGenreArtistCountAsync(IDbConnection connection, long genreId, IDbTransaction transaction, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(UpdateGenreArtistCountSql, new { genreId }, transaction, cancellationToken: cancellationToken));

    /// <summary>Recomputes the artist count of the genres carried by the given artists.</summary>
    public static async Task UpdateGenresOfArtistsAsync(IDbConnection connection, IReadOnlyCollection<long> artistIds, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        if (artistIds.Count == 0)
            return;

        long[] genreIds = [.. await connection.QueryAsync<long>(new CommandDefinition(SelectGenresOfArtistsSql, new { artistIds }, transaction, cancellationToken: cancellationToken))];

        foreach (long genreId in genreIds)
        {
            await UpdateGenreArtistCountAsync(connection, genreId, transaction, cancellationToken);
        }
    }

    /// <summary>Recomputes the album counters (albums, best of, live, compilations) of a genre.</summary>
    public static Task UpdateGenreAlbumCountersAsync(IDbConnection connection, long genreId, IDbTransaction transaction, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(UpdateGenreAlbumCountersSql, new { genreId }, transaction, cancellationToken: cancellationToken));

    /// <summary>Recomputes every counter of a genre: tracks, duration, artists and albums.</summary>
    public static async Task UpdateGenreAsync(IDbConnection connection, long genreId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(UpdateGenreTrackCountersSql, new { genreId }, transaction, cancellationToken: cancellationToken));
        await UpdateGenreArtistCountAsync(connection, genreId, transaction, cancellationToken);
        await UpdateGenreAlbumCountersAsync(connection, genreId, transaction, cancellationToken);
    }
}