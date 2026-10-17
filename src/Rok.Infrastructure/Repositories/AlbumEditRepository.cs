using Microsoft.Extensions.Logging;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Interfaces.Repositories;
using Rok.Infrastructure.Repositories.Sql;

namespace Rok.Infrastructure.Repositories;

public class AlbumEditRepository(IDbConnection connection, IPendingTagWriteRepository pendingTagWrites, TimeProvider timeProvider, ILogger<AlbumEditRepository> logger) : IAlbumEditRepository
{
    private const string InsertGenreSql = """
        INSERT INTO genres(name, totalDurationSeconds, trackCount, artistCount, compilationCount, bestofCount, albumCount, liveCount, listenCount, isFavorite, creatDate)
        VALUES (@name, 0, 0, 0, 0, 0, 0, 0, 0, 0, @now);
        SELECT last_insert_rowid();
        """;

    private const string InsertArtistSql = """
        INSERT INTO artists(name, genreId, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
        VALUES (@name, @genreId, 0, 0, 0, 0, 0, 0, 0, 0, 0, @now);
        SELECT last_insert_rowid();
        """;

    private const string UpdateAlbumSql = "UPDATE albums SET name = @name, artistId = @artistId, genreId = @genreId, year = COALESCE(@year, year), editDate = @now WHERE id = @albumId";
    private const string SelectArtistTracksSql = "SELECT id FROM tracks WHERE albumId = @albumId AND artistId = @oldArtistId";
    private const string MoveTracksToArtistSql = "UPDATE tracks SET artistId = @newArtistId WHERE id IN @trackIds";
    private const string MoveEventsToArtistSql = "UPDATE ListeningEvents SET artistId = @newArtistId WHERE trackId IN @trackIds";
    private const string SelectGenreTracksSql = "SELECT id FROM tracks WHERE albumId = @albumId AND genreId IS @oldGenreId";
    private const string MoveTracksToGenreSql = "UPDATE tracks SET genreId = @newGenreId WHERE id IN @trackIds";
    private const string MoveEventsToGenreSql = "UPDATE ListeningEvents SET genreId = @newGenreId WHERE trackId IN @trackIds";

    public async Task<AlbumEditApplied> ApplyAsync(AlbumEditDbChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(change.PendingWrites);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.AlbumId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.OldArtistId);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.Name);

        if (change.NewArtistName is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(change.NewArtistName);

        if (change.NewGenreName is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(change.NewGenreName);

        if (connection.State == ConnectionState.Closed)
            connection.Open();

        using IDbTransaction transaction = connection.BeginTransaction();

        try
        {
            AlbumEditApplied applied = await ApplyInTransactionAsync(change, transaction, cancellationToken);

            transaction.Commit();

            return applied;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Edit of album {AlbumId} failed, rolling back", change.AlbumId);
            transaction.Rollback();
            throw;
        }
    }

    private async Task<AlbumEditApplied> ApplyInTransactionAsync(AlbumEditDbChange change, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        long albumId = change.AlbumId;

        long? genreId = change.NewGenreId ?? change.OldGenreId;

        if (change.NewGenreName is not null)
            genreId = await connection.ExecuteScalarAsync<long>(Command(InsertGenreSql, new { name = change.NewGenreName, now }, transaction, cancellationToken));

        long artistId = change.NewArtistId ?? change.OldArtistId;

        if (change.NewArtistName is not null)
            artistId = await connection.ExecuteScalarAsync<long>(Command(InsertArtistSql, new { name = change.NewArtistName, genreId, now }, transaction, cancellationToken));

        int updated = await connection.ExecuteAsync(Command(UpdateAlbumSql, new { name = change.Name, artistId, genreId, year = change.Year, now, albumId }, transaction, cancellationToken));

        if (updated != 1)
            throw new InvalidOperationException($"Album {albumId} not found.");

        if (artistId != change.OldArtistId)
            await MoveTracksToArtistAsync(albumId, change.OldArtistId, artistId, transaction, cancellationToken);

        if (genreId != change.OldGenreId)
            await MoveTracksToGenreAsync(albumId, change.OldGenreId, genreId, transaction, cancellationToken);

        foreach (PendingTagWrite pending in change.PendingWrites)
        {
            await pendingTagWrites.AddAsync(pending.TrackId, pending.Fields, transaction, cancellationToken);
        }

        await UpdateCountersAsync(albumId, [change.OldArtistId, artistId], [change.OldGenreId, genreId], transaction, cancellationToken);

        return new AlbumEditApplied(artistId, genreId);
    }

    private async Task MoveTracksToArtistAsync(long albumId, long oldArtistId, long newArtistId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        long[] trackIds = [.. await connection.QueryAsync<long>(Command(SelectArtistTracksSql, new { albumId, oldArtistId }, transaction, cancellationToken))];

        if (trackIds.Length == 0)
            return;

        await connection.ExecuteAsync(Command(MoveTracksToArtistSql, new { newArtistId, trackIds }, transaction, cancellationToken));
        await connection.ExecuteAsync(Command(MoveEventsToArtistSql, new { newArtistId, trackIds }, transaction, cancellationToken));
    }

    private async Task MoveTracksToGenreAsync(long albumId, long? oldGenreId, long? newGenreId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        long[] trackIds = [.. await connection.QueryAsync<long>(Command(SelectGenreTracksSql, new { albumId, oldGenreId }, transaction, cancellationToken))];

        if (trackIds.Length == 0)
            return;

        await connection.ExecuteAsync(Command(MoveTracksToGenreSql, new { newGenreId, trackIds }, transaction, cancellationToken));
        await connection.ExecuteAsync(Command(MoveEventsToGenreSql, new { newGenreId, trackIds }, transaction, cancellationToken));
    }

    private async Task UpdateCountersAsync(long albumId, IEnumerable<long> artistIds, IEnumerable<long?> genreIds, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        await LibraryCounterSql.UpdateAlbumAsync(connection, albumId, transaction, cancellationToken);

        long[] distinctArtistIds = [.. artistIds.Distinct()];

        foreach (long artistId in distinctArtistIds)
        {
            await LibraryCounterSql.UpdateArtistAsync(connection, artistId, transaction, cancellationToken);
        }

        await LibraryCounterSql.UpdateGenresOfArtistsAsync(connection, distinctArtistIds, transaction, cancellationToken);

        foreach (long genreId in genreIds.OfType<long>().Distinct())
        {
            await LibraryCounterSql.UpdateGenreAsync(connection, genreId, transaction, cancellationToken);
        }
    }

    private static CommandDefinition Command(string sql, object parameters, IDbTransaction transaction, CancellationToken cancellationToken) =>
        new(sql, parameters, transaction, cancellationToken: cancellationToken);
}