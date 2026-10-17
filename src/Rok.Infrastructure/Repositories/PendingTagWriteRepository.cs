using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Infrastructure.Repositories;

/// <summary>Dapper repository of <c>pendingTagWrites</c>, on the shared connection.</summary>
public class PendingTagWriteRepository(IDbConnection connection, ILogger<PendingTagWriteRepository> logger) : IPendingTagWriteRepository
{
    private const string SelectByAlbumSql = "SELECT p.trackId AS TrackId, p.fields AS Fields FROM pendingTagWrites p INNER JOIN tracks t ON t.id = p.trackId WHERE t.albumId = @albumId ORDER BY p.trackId";
    private const string CountByAlbumSql = "SELECT COUNT(*) FROM pendingTagWrites p INNER JOIN tracks t ON t.id = p.trackId WHERE t.albumId = @albumId";
    private const string UpsertSql = "INSERT INTO pendingTagWrites(trackId, fields) VALUES (@trackId, @fields) ON CONFLICT(trackId) DO UPDATE SET fields = fields | excluded.fields";
    private const string UpdateFileInfoSql = "UPDATE tracks SET fileDate = @fileDate, size = @size WHERE id = @trackId";
    private const string DeleteSql = "DELETE FROM pendingTagWrites WHERE trackId = @trackId";
    private const string DeleteOrphansSql = "DELETE FROM pendingTagWrites WHERE trackId NOT IN (SELECT id FROM tracks)";

    public async Task<IReadOnlyList<PendingTagWrite>> GetByAlbumAsync(long albumId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(albumId);

        IEnumerable<PendingRow> rows = await connection.QueryAsync<PendingRow>(new CommandDefinition(SelectByAlbumSql, new { albumId }, cancellationToken: cancellationToken));

        return rows.Select(r => new PendingTagWrite(r.TrackId, (ETagWriteFields)(int)r.Fields)).ToList();
    }

    public Task<int> CountByAlbumAsync(long albumId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(albumId);

        return connection.ExecuteScalarAsync<int>(new CommandDefinition(CountByAlbumSql, new { albumId }, cancellationToken: cancellationToken));
    }

    public async Task AddAsync(long trackId, ETagWriteFields fields, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trackId);

        if (fields == ETagWriteFields.None)
            return;

        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new { trackId, fields = (int)fields }, transaction, cancellationToken: cancellationToken));
    }

    public async Task CompleteAsync(long trackId, DateTime fileDate, long size, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trackId);

        if (connection.State == ConnectionState.Closed)
            connection.Open();

        using IDbTransaction transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(UpdateFileInfoSql, new { fileDate, size, trackId }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(DeleteSql, new { trackId }, transaction, cancellationToken: cancellationToken));

            transaction.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Completing the pending tag write of track {TrackId} failed, rolling back", trackId);
            transaction.Rollback();
            throw;
        }
    }

    public Task<int> DeleteOrphansAsync(CancellationToken cancellationToken = default)
    {
        return connection.ExecuteAsync(new CommandDefinition(DeleteOrphansSql, cancellationToken: cancellationToken));
    }

    private sealed record PendingRow(long TrackId, long Fields);
}