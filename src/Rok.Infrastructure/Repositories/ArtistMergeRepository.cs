using Microsoft.Extensions.Logging;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Infrastructure.Repositories;

public class ArtistMergeRepository(IDbConnection connection, ILogger<ArtistMergeRepository> logger) : IArtistMergeRepository
{
    private const string UpdateTargetSql = """
        UPDATE artists SET
            musicBrainzID = @MusicBrainzID, pictureDominantColor = @PictureDominantColor,
            wikipediaUrl = @WikipediaUrl, officialSiteUrl = @OfficialSiteUrl, facebookUrl = @FacebookUrl, twitterUrl = @TwitterUrl,
            flickrUrl = @FlickrUrl, instagramUrl = @InstagramUrl, tiktokUrl = @TiktokUrl, threadsUrl = @ThreadsUrl,
            songkickUrl = @SongkickUrl, soundcloundUrl = @SoundcloundUrl, imdbUrl = @ImdbUrl, lastFmUrl = @LastFmUrl,
            discogsUrl = @DiscogsUrl, bandsintownUrl = @BandsintownUrl, youtubeUrl = @YoutubeUrl, allMusicUrl = @AllMusicUrl,
            audioDbID = @AudioDbID, formedYear = @FormedYear, bornYear = @BornYear, diedYear = @DiedYear, disbanded = @Disbanded,
            members = @Members, similarArtists = @SimilarArtists, biography = @Biography, genreId = @GenreId, countryId = @CountryId,
            isFavorite = @IsFavorite, isLock = @IsLock, listenCount = @ListenCount, lastListen = @LastListen,
            renamePendingFrom = @pendingFrom, getMetaDataLastAttempt = NULL
        WHERE id = @targetId
        """;

    private const string SelectSourceGenreSql = "SELECT genreId FROM artists WHERE id = @sourceId";
    private const string MoveTracksSql = "UPDATE tracks SET artistId = @targetId WHERE artistId = @sourceId";
    private const string MoveAlbumsSql = "UPDATE albums SET artistId = @targetId WHERE artistId = @sourceId";
    private const string ConvertCompilationsSql = "UPDATE albums SET isCompilation = 0, artistId = @targetId WHERE id IN @ids";
    private const string MoveAlbumTracksSql = "UPDATE tracks SET albumId = @survivorId WHERE albumId IN @mergedIds";
    private const string MoveAlbumEventsSql = "UPDATE ListeningEvents SET albumId = @survivorId WHERE albumId IN @mergedIds";
    private const string UnionAlbumTagsSql = "INSERT OR IGNORE INTO AlbumTags(albumId, tagId) SELECT @survivorId, tagId FROM AlbumTags WHERE albumId IN @mergedIds";
    private const string DeleteAlbumTagsSql = "DELETE FROM AlbumTags WHERE albumId IN @mergedIds";
    private const string UpdateSurvivorSql = "UPDATE albums SET isFavorite = @isFavorite, listenCount = @listenCount, lastListen = @lastListen WHERE id = @survivorId";
    private const string DeleteAlbumsSql = "DELETE FROM albums WHERE id IN @mergedIds";
    private const string MoveArtistEventsSql = "UPDATE ListeningEvents SET artistId = @targetId WHERE artistId = @sourceId";
    private const string UnionArtistTagsSql = "INSERT OR IGNORE INTO ArtistTags(artistId, tagId) SELECT @targetId, tagId FROM ArtistTags WHERE artistId = @sourceId";
    private const string DeleteArtistTagsSql = "DELETE FROM ArtistTags WHERE artistId = @sourceId";
    private const string CountPresetsSql = "SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = @scope AND ScopeId = @scopeId";
    private const string DeletePresetSql = "DELETE FROM EqualizerPreset WHERE Scope = @scope AND ScopeId = @scopeId";
    private const string MovePresetSql = "UPDATE EqualizerPreset SET ScopeId = @scopeId WHERE Scope = @scope AND ScopeId = @fromId";
    private const string SelectSmallestPresetOwnerSql = "SELECT MIN(ScopeId) FROM EqualizerPreset WHERE Scope = @scope AND ScopeId IN @ids";
    private const string DeletePresetsOfSql = "DELETE FROM EqualizerPreset WHERE Scope = @scope AND ScopeId IN @ids";
    private const string UpdatePlaylistPictureSql = "UPDATE playlists SET picture = @targetName WHERE picture = @sourceName COLLATE NOCASE";
    private const string UpdatePlaylistCriteriaSql = "UPDATE playlists SET groupsJson = @groupsJson WHERE id = @playlistId";
    private const string UpdateAlbumStatisticsSql = "UPDATE albums SET trackCount = (SELECT COUNT(*) FROM tracks WHERE albumId = @survivorId), duration = (SELECT COALESCE(SUM(duration), 0) FROM tracks WHERE albumId = @survivorId) WHERE id = @survivorId";

    private const string UpdateTargetStatisticsSql = """
        UPDATE artists SET
            trackCount = (SELECT COUNT(*) FROM tracks WHERE artistId = @targetId),
            totalDurationSeconds = (SELECT COALESCE(SUM(duration), 0) FROM tracks WHERE artistId = @targetId),
            albumCount = (SELECT COUNT(*) FROM albums WHERE artistId = @targetId AND isCompilation = 0 AND isLive = 0 AND isBestof = 0),
            bestofCount = (SELECT COUNT(*) FROM albums WHERE artistId = @targetId AND isBestof = 1 AND isCompilation = 0),
            liveCount = (SELECT COUNT(*) FROM albums WHERE artistId = @targetId AND isLive = 1 AND isCompilation = 0),
            compilationCount = (SELECT COUNT(*) FROM albums WHERE artistId = @targetId AND isCompilation = 1),
            yearMini = NULLIF((SELECT MIN(year) FROM albums WHERE artistId = @targetId AND year IS NOT NULL AND isCompilation = 0 AND isLive = 0 AND isBestof = 0), 0),
            yearMaxi = NULLIF((SELECT MAX(year) FROM albums WHERE artistId = @targetId AND year IS NOT NULL AND isCompilation = 0 AND isLive = 0 AND isBestof = 0), 0)
        WHERE id = @targetId
        """;

    private const string SelectGenresOfAlbumsSql = "SELECT DISTINCT genreId FROM albums WHERE id IN @ids AND genreId IS NOT NULL";
    private const string UpdateGenreArtistCountSql = "UPDATE genres SET artistCount = (SELECT COUNT(*) FROM artists WHERE genreId = @genreId) WHERE id = @genreId";

    private const string UpdateGenreAlbumCountersSql = """
        UPDATE genres SET
            albumCount = (SELECT COUNT(*) FROM albums WHERE genreId = @genreId AND isCompilation = 0 AND isLive = 0 AND isBestof = 0),
            bestofCount = (SELECT COUNT(*) FROM albums WHERE genreId = @genreId AND isBestof = 1 AND isCompilation = 0),
            liveCount = (SELECT COUNT(*) FROM albums WHERE genreId = @genreId AND isLive = 1 AND isCompilation = 0),
            compilationCount = (SELECT COUNT(*) FROM albums WHERE genreId = @genreId AND isCompilation = 1)
        WHERE id = @genreId
        """;

    private const string DeleteSourceSql = "DELETE FROM artists WHERE id = @sourceId";

    public async Task ApplyAsync(ArtistMergeDbChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(change.Merged);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.SourceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.TargetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.SourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.TargetName);

        if (change.SourceId == change.TargetId)
            throw new ArgumentException("Source and target must be different artists.", nameof(change));

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
            logger.LogError(ex, "Merge of artist {SourceId} into {TargetId} failed, rolling back", change.SourceId, change.TargetId);
            transaction.Rollback();
            throw;
        }
    }

    private async Task ApplyInTransactionAsync(ArtistMergeDbChange change, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        long sourceId = change.SourceId;
        long targetId = change.TargetId;

        long? sourceGenreId = await connection.ExecuteScalarAsync<long?>(Command(SelectSourceGenreSql, new { sourceId }, transaction, cancellationToken));

        int updated = await connection.ExecuteAsync(Command(UpdateTargetSql, TargetParameters(change), transaction, cancellationToken));

        if (updated != 1)
            throw new InvalidOperationException($"Artist {targetId} not found.");

        await ExecuteAsync(MoveTracksSql, new { targetId, sourceId }, transaction, cancellationToken);
        await ExecuteAsync(MoveAlbumsSql, new { targetId, sourceId }, transaction, cancellationToken);

        long[] convertedIds = [.. change.ConvertedCompilationIds ?? []];

        if (convertedIds.Length > 0)
            await ExecuteAsync(ConvertCompilationsSql, new { targetId, ids = convertedIds }, transaction, cancellationToken);

        long[] touchedAlbumIds = [.. change.AlbumGroups.SelectMany(g => g.MergedIds).Concat(convertedIds).Distinct()];
        long[] albumGenreIds = touchedAlbumIds.Length == 0
            ? []
            : [.. await connection.QueryAsync<long>(Command(SelectGenresOfAlbumsSql, new { ids = touchedAlbumIds }, transaction, cancellationToken))];

        foreach (AlbumMergeGroup group in change.AlbumGroups)
        {
            await MergeAlbumGroupAsync(group, transaction, cancellationToken);
        }

        await ExecuteAsync(MoveArtistEventsSql, new { targetId, sourceId }, transaction, cancellationToken);
        await ExecuteAsync(UnionArtistTagsSql, new { targetId, sourceId }, transaction, cancellationToken);
        await ExecuteAsync(DeleteArtistTagsSql, new { sourceId }, transaction, cancellationToken);

        await MoveArtistPresetAsync(sourceId, targetId, transaction, cancellationToken);

        await ExecuteAsync(UpdatePlaylistPictureSql, new { targetName = change.TargetName, sourceName = change.SourceName }, transaction, cancellationToken);

        foreach (PlaylistCriteriaChange criteria in change.Criteria)
        {
            await ExecuteAsync(UpdatePlaylistCriteriaSql, new { groupsJson = criteria.GroupsJson, playlistId = criteria.PlaylistId }, transaction, cancellationToken);
        }

        await UpdateArtistAndAlbumCountersAsync(change, transaction, cancellationToken);

        int deleted = await connection.ExecuteAsync(Command(DeleteSourceSql, new { sourceId }, transaction, cancellationToken));

        if (deleted != 1)
            throw new InvalidOperationException($"Artist {sourceId} not found.");

        await UpdateGenreCountersAsync(sourceGenreId, change.Merged.GenreId, albumGenreIds, transaction, cancellationToken);
    }

    private async Task MergeAlbumGroupAsync(AlbumMergeGroup group, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        long survivorId = group.SurvivorId;
        long[] mergedIds = [.. group.MergedIds];

        if (mergedIds.Length == 0)
            return;

        await ExecuteAsync(MoveAlbumTracksSql, new { survivorId, mergedIds }, transaction, cancellationToken);
        await ExecuteAsync(MoveAlbumEventsSql, new { survivorId, mergedIds }, transaction, cancellationToken);
        await ExecuteAsync(UnionAlbumTagsSql, new { survivorId, mergedIds }, transaction, cancellationToken);
        await ExecuteAsync(DeleteAlbumTagsSql, new { mergedIds }, transaction, cancellationToken);

        string scope = nameof(EqualizerScope.Album);
        bool survivorHasPreset = await connection.ExecuteScalarAsync<int>(Command(CountPresetsSql, new { scope, scopeId = survivorId }, transaction, cancellationToken)) > 0;

        if (!survivorHasPreset)
        {
            long? presetOwner = await connection.ExecuteScalarAsync<long?>(Command(SelectSmallestPresetOwnerSql, new { scope, ids = mergedIds }, transaction, cancellationToken));

            if (presetOwner is not null)
                await ExecuteAsync(MovePresetSql, new { scope, scopeId = survivorId, fromId = presetOwner.Value }, transaction, cancellationToken);
        }

        await ExecuteAsync(DeletePresetsOfSql, new { scope, ids = mergedIds }, transaction, cancellationToken);
        await ExecuteAsync(UpdateSurvivorSql, new { isFavorite = group.IsFavorite, listenCount = group.ListenCount, lastListen = group.LastListen, survivorId }, transaction, cancellationToken);
        await ExecuteAsync(DeleteAlbumsSql, new { mergedIds }, transaction, cancellationToken);
    }

    private async Task MoveArtistPresetAsync(long sourceId, long targetId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        string scope = nameof(EqualizerScope.Artist);
        bool targetHasPreset = await connection.ExecuteScalarAsync<int>(Command(CountPresetsSql, new { scope, scopeId = targetId }, transaction, cancellationToken)) > 0;

        if (targetHasPreset)
        {
            await ExecuteAsync(DeletePresetSql, new { scope, scopeId = sourceId }, transaction, cancellationToken);

            return;
        }

        await ExecuteAsync(MovePresetSql, new { scope, scopeId = targetId, fromId = sourceId }, transaction, cancellationToken);
    }

    private async Task UpdateArtistAndAlbumCountersAsync(ArtistMergeDbChange change, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        foreach (AlbumMergeGroup group in change.AlbumGroups)
        {
            await ExecuteAsync(UpdateAlbumStatisticsSql, new { survivorId = group.SurvivorId }, transaction, cancellationToken);
        }

        await ExecuteAsync(UpdateTargetStatisticsSql, new { targetId = change.TargetId }, transaction, cancellationToken);
    }

    private async Task UpdateGenreCountersAsync(long? sourceGenreId, long? targetGenreId, IReadOnlyCollection<long> mergedAlbumGenreIds, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        foreach (long genreId in new[] { sourceGenreId, targetGenreId }.OfType<long>().Distinct())
        {
            await ExecuteAsync(UpdateGenreArtistCountSql, new { genreId }, transaction, cancellationToken);
        }

        foreach (long genreId in mergedAlbumGenreIds.Concat(new[] { sourceGenreId, targetGenreId }.OfType<long>()).Distinct())
        {
            await ExecuteAsync(UpdateGenreAlbumCountersSql, new { genreId }, transaction, cancellationToken);
        }
    }

    private static DynamicParameters TargetParameters(ArtistMergeDbChange change)
    {
        DynamicParameters parameters = new(change.Merged);
        parameters.Add("pendingFrom", change.PendingFrom);
        parameters.Add("targetId", change.TargetId);

        return parameters;
    }

    private Task<int> ExecuteAsync(string sql, object parameters, IDbTransaction transaction, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(Command(sql, parameters, transaction, cancellationToken));

    private static CommandDefinition Command(string sql, object parameters, IDbTransaction transaction, CancellationToken cancellationToken) =>
        new(sql, parameters, transaction, cancellationToken: cancellationToken);
}