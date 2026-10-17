using Rok.Application.Features.Artists.Merge;
using Rok.Domain.Enums;

namespace Rok.Infrastructure.Repositories.Sql;

/// <summary>
/// SQL that folds albums into a survivor inside an open transaction of the shared connection: tracks, listening history,
/// tags and equalizer preset move to the survivor, then the folded albums are deleted.
/// </summary>
internal static class AlbumGroupMergeSql
{
    private const string MoveTracksSql = "UPDATE tracks SET albumId = @survivorId WHERE albumId IN @mergedIds";
    private const string MoveEventsSql = "UPDATE ListeningEvents SET albumId = @survivorId WHERE albumId IN @mergedIds";
    private const string UnionTagsSql = "INSERT OR IGNORE INTO AlbumTags(albumId, tagId) SELECT @survivorId, tagId FROM AlbumTags WHERE albumId IN @mergedIds";
    private const string DeleteTagsSql = "DELETE FROM AlbumTags WHERE albumId IN @mergedIds";
    private const string CountPresetsSql = "SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = @scope AND ScopeId = @scopeId";
    private const string SelectSmallestPresetOwnerSql = "SELECT MIN(ScopeId) FROM EqualizerPreset WHERE Scope = @scope AND ScopeId IN @ids";
    private const string MovePresetSql = "UPDATE EqualizerPreset SET ScopeId = @scopeId WHERE Scope = @scope AND ScopeId = @fromId";
    private const string DeletePresetsOfSql = "DELETE FROM EqualizerPreset WHERE Scope = @scope AND ScopeId IN @ids";
    private const string UpdateSurvivorSql = "UPDATE albums SET isFavorite = @isFavorite, listenCount = @listenCount, lastListen = @lastListen WHERE id = @survivorId";
    private const string DeleteAlbumsSql = "DELETE FROM albums WHERE id IN @mergedIds";

    /// <summary>Folds the merged albums of the group into its survivor. Does nothing when the group has no merged album.</summary>
    public static async Task MergeAsync(IDbConnection connection, AlbumMergeGroup group, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        long survivorId = group.SurvivorId;
        long[] mergedIds = [.. group.MergedIds];

        if (mergedIds.Length == 0)
            return;

        await ExecuteAsync(connection, MoveTracksSql, new { survivorId, mergedIds }, transaction, cancellationToken);
        await ExecuteAsync(connection, MoveEventsSql, new { survivorId, mergedIds }, transaction, cancellationToken);
        await ExecuteAsync(connection, UnionTagsSql, new { survivorId, mergedIds }, transaction, cancellationToken);
        await ExecuteAsync(connection, DeleteTagsSql, new { mergedIds }, transaction, cancellationToken);

        string scope = nameof(EqualizerScope.Album);
        bool survivorHasPreset = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountPresetsSql, new { scope, scopeId = survivorId }, transaction, cancellationToken: cancellationToken)) > 0;

        if (!survivorHasPreset)
        {
            long? presetOwner = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(SelectSmallestPresetOwnerSql, new { scope, ids = mergedIds }, transaction, cancellationToken: cancellationToken));

            if (presetOwner is not null)
                await ExecuteAsync(connection, MovePresetSql, new { scope, scopeId = survivorId, fromId = presetOwner.Value }, transaction, cancellationToken);
        }

        await ExecuteAsync(connection, DeletePresetsOfSql, new { scope, ids = mergedIds }, transaction, cancellationToken);
        await ExecuteAsync(connection, UpdateSurvivorSql, new { isFavorite = group.IsFavorite, listenCount = group.ListenCount, lastListen = group.LastListen, survivorId }, transaction, cancellationToken);
        await ExecuteAsync(connection, DeleteAlbumsSql, new { mergedIds }, transaction, cancellationToken);
    }

    private static Task<int> ExecuteAsync(IDbConnection connection, string sql, object parameters, IDbTransaction transaction, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
}