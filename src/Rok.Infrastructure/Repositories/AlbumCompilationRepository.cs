using Microsoft.Extensions.Logging;
using Rok.Application.Features.Albums.Compilation;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories.Sql;

namespace Rok.Infrastructure.Repositories;

public class AlbumCompilationRepository(IDbConnection connection, IPendingTagWriteRepository pendingTagWrites, TimeProvider timeProvider, ILogger<AlbumCompilationRepository> logger) : IAlbumCompilationRepository
{
    private const string UpdateAlbumSql = "UPDATE albums SET isCompilation = @isCompilation, artistId = @artistId, compilationSource = @compilationSource, editDate = @now WHERE id = @albumId";

    public async Task ApplyAsync(AlbumCompilationDbChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(change.PendingWrites);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.AlbumId);

        if (connection.State == ConnectionState.Closed)
            connection.Open();

        using IDbTransaction transaction = connection.BeginTransaction();

        try
        {
            await ApplyInTransactionAsync(change, transaction, cancellationToken);

            transaction.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Compilation status change of album {AlbumId} failed, rolling back", change.AlbumId);
            transaction.Rollback();
            throw;
        }
    }

    private async Task ApplyInTransactionAsync(AlbumCompilationDbChange change, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        long albumId = change.AlbumId;

        int updated = await connection.ExecuteAsync(new CommandDefinition(
            UpdateAlbumSql,
            new { isCompilation = change.IsCompilation, artistId = change.ArtistId, compilationSource = (int)ECompilationSource.Manual, now, albumId },
            transaction,
            cancellationToken: cancellationToken));

        if (updated != 1)
            throw new InvalidOperationException($"Album {albumId} not found.");

        foreach (PendingTagWrite pending in change.PendingWrites)
        {
            await pendingTagWrites.AddAsync(pending.TrackId, pending.Fields, transaction, cancellationToken);
        }

        await LibraryCounterSql.UpdateAlbumAsync(connection, albumId, transaction, cancellationToken);

        if (change.ArtistId is long artistId)
        {
            await LibraryCounterSql.UpdateArtistAsync(connection, artistId, transaction, cancellationToken);
            await LibraryCounterSql.UpdateGenresOfArtistsAsync(connection, [artistId], transaction, cancellationToken);
        }

        if (change.GenreId is long genreId)
            await LibraryCounterSql.UpdateGenreAsync(connection, genreId, transaction, cancellationToken);
    }
}