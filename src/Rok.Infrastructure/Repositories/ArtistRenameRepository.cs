using Microsoft.Extensions.Logging;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

public class ArtistRenameRepository(IDbConnection connection, ILogger<ArtistRenameRepository> logger) : IArtistRenameRepository
{
    private const string UpdateArtistSql = "UPDATE artists SET name = @newName, renamePendingFrom = COALESCE(renamePendingFrom, @oldName), getMetaDataLastAttempt = NULL WHERE id = @artistId";
    private const string UpdatePlaylistPictureSql = "UPDATE playlists SET picture = @newName WHERE picture = @oldName COLLATE NOCASE";
    private const string UpdatePlaylistCriteriaSql = "UPDATE playlists SET groupsJson = @groupsJson WHERE id = @playlistId";
    private const string ClearMarkerSql = "UPDATE artists SET renamePendingFrom = NULL WHERE id = @artistId";

    public async Task ApplyAsync(ArtistRenameDbChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.ArtistId);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.NewName);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.OldName);

        if (connection.State == ConnectionState.Closed)
            connection.Open();

        using IDbTransaction transaction = connection.BeginTransaction();

        try
        {
            int updated = await connection.ExecuteAsync(new CommandDefinition(UpdateArtistSql, new { newName = change.NewName, oldName = change.OldName, artistId = change.ArtistId }, transaction, cancellationToken: cancellationToken));

            if (updated != 1)
                throw new InvalidOperationException($"Artist {change.ArtistId} not found.");

            await connection.ExecuteAsync(new CommandDefinition(UpdatePlaylistPictureSql, new { newName = change.NewName, oldName = change.OldName }, transaction, cancellationToken: cancellationToken));

            foreach (PlaylistCriteriaChange criteria in change.Criteria)
            {
                await connection.ExecuteAsync(new CommandDefinition(UpdatePlaylistCriteriaSql, new { groupsJson = criteria.GroupsJson, playlistId = criteria.PlaylistId }, transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Artist {ArtistId} rename to {NewName} failed, rolling back", change.ArtistId, change.NewName);
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> CompleteAsync(long artistId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(artistId);

        int updated = await connection.ExecuteAsync(new CommandDefinition(ClearMarkerSql, new { artistId }, cancellationToken: cancellationToken));

        return updated > 0;
    }
}