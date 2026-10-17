using Microsoft.Extensions.Logging;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Interfaces.Repositories;
using Rok.Infrastructure.Repositories.Sql;

namespace Rok.Infrastructure.Repositories;

public class AlbumMergeRepository(IDbConnection connection, IPendingTagWriteRepository pendingTagWrites, TimeProvider timeProvider, ILogger<AlbumMergeRepository> logger) : IAlbumMergeRepository
{
    private const string SelectAlbumSql = "SELECT artistId, genreId FROM albums WHERE id = @albumId";

    private const string UpdateTargetSql = """
        UPDATE albums SET
            musicBrainzID = @MusicBrainzID, releaseGroupMusicBrainzID = @ReleaseGroupMusicBrainzID, audioDbID = @AudioDbID,
            audioDbArtistID = @AudioDbArtistID, allMusicID = @AllMusicID, discogsID = @DiscogsID, musicMozID = @MusicMozID,
            lyricWikiID = @LyricWikiID, geniusID = @GeniusID, wikipediaID = @WikipediaID, wikidataID = @WikidataID, amazonID = @AmazonID,
            wikipedia = @Wikipedia, biography = @Biography, lastFmUrl = @LastFmUrl, label = @Label, sales = @Sales,
            releaseFormat = @ReleaseFormat, musicBrainzReleaseType = @MusicBrainzReleaseType, musicBrainzReleaseCountry = @MusicBrainzReleaseCountry,
            year = @Year, genreId = @GenreId, releaseDate = @ReleaseDate, isLock = @IsLock, editDate = @now
        WHERE id = @targetId
        """;

    private const string SelectArtistTracksSql = "SELECT id FROM tracks WHERE albumId = @sourceId AND artistId = @sourceArtistId";
    private const string MoveTracksToArtistSql = "UPDATE tracks SET artistId = @targetArtistId WHERE id IN @trackIds";
    private const string MoveEventsToArtistSql = "UPDATE ListeningEvents SET artistId = @targetArtistId WHERE trackId IN @trackIds";
    private const string ShiftDiscsSql = "UPDATE tracks SET disc = COALESCE(NULLIF(disc, 0), 1) + @offset WHERE albumId = @sourceId";
    private const string UpdateDiscCountSql = "UPDATE albums SET discCount = (SELECT MAX(COALESCE(NULLIF(disc, 0), 1)) FROM tracks WHERE albumId = @targetId) WHERE id = @targetId";

    public async Task ApplyAsync(AlbumMergeDbChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(change.Merged);
        ArgumentNullException.ThrowIfNull(change.PendingWrites);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.SourceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.TargetId);
        ArgumentOutOfRangeException.ThrowIfNegative(change.DiscOffset);

        if (change.SourceId == change.TargetId)
            throw new ArgumentException("Source and target must be different albums.", nameof(change));

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
            logger.LogError(ex, "Merge of album {SourceId} into {TargetId} failed, rolling back", change.SourceId, change.TargetId);
            transaction.Rollback();
            throw;
        }
    }

    private async Task ApplyInTransactionAsync(AlbumMergeDbChange change, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        long sourceId = change.SourceId;
        long targetId = change.TargetId;
        AlbumEntity merged = change.Merged;

        AlbumOwner source = await ReadAlbumAsync(sourceId, transaction, cancellationToken);
        AlbumOwner target = await ReadAlbumAsync(targetId, transaction, cancellationToken);

        long targetArtistId = merged.ArtistId ?? target.ArtistId ?? throw new InvalidOperationException($"Album {targetId} has no artist.");

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        int updated = await connection.ExecuteAsync(Command(UpdateTargetSql, TargetParameters(merged, targetId, now), transaction, cancellationToken));

        if (updated != 1)
            throw new InvalidOperationException($"Album {targetId} not found.");

        if (source.ArtistId is long sourceArtistId && sourceArtistId != targetArtistId)
            await MoveTracksToArtistAsync(sourceId, sourceArtistId, targetArtistId, transaction, cancellationToken);

        if (change.DiscOffset > 0)
            await connection.ExecuteAsync(Command(ShiftDiscsSql, new { offset = change.DiscOffset, sourceId }, transaction, cancellationToken));

        AlbumMergeGroup group = new(targetId, [sourceId], merged.Name, merged.IsFavorite, merged.ListenCount, merged.LastListen);
        await AlbumGroupMergeSql.MergeAsync(connection, group, transaction, cancellationToken);

        if (change.DiscOffset > 0)
            await connection.ExecuteAsync(Command(UpdateDiscCountSql, new { targetId }, transaction, cancellationToken));

        foreach (PendingTagWrite pending in change.PendingWrites)
        {
            await pendingTagWrites.AddAsync(pending.TrackId, pending.Fields, transaction, cancellationToken);
        }

        await UpdateCountersAsync(targetId, [source.ArtistId, target.ArtistId, targetArtistId], [source.GenreId, target.GenreId, merged.GenreId], transaction, cancellationToken);
    }

    private async Task<AlbumOwner> ReadAlbumAsync(long albumId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        AlbumOwner? owner = await connection.QuerySingleOrDefaultAsync<AlbumOwner>(Command(SelectAlbumSql, new { albumId }, transaction, cancellationToken));

        return owner ?? throw new InvalidOperationException($"Album {albumId} not found.");
    }

    private async Task MoveTracksToArtistAsync(long sourceId, long sourceArtistId, long targetArtistId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        long[] trackIds = [.. await connection.QueryAsync<long>(Command(SelectArtistTracksSql, new { sourceId, sourceArtistId }, transaction, cancellationToken))];

        if (trackIds.Length == 0)
            return;

        await connection.ExecuteAsync(Command(MoveTracksToArtistSql, new { targetArtistId, trackIds }, transaction, cancellationToken));
        await connection.ExecuteAsync(Command(MoveEventsToArtistSql, new { targetArtistId, trackIds }, transaction, cancellationToken));
    }

    private async Task UpdateCountersAsync(long targetId, IEnumerable<long?> artistIds, IEnumerable<long?> genreIds, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        await LibraryCounterSql.UpdateAlbumAsync(connection, targetId, transaction, cancellationToken);

        long[] distinctArtistIds = [.. artistIds.OfType<long>().Distinct()];

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

    private static object TargetParameters(AlbumEntity merged, long targetId, DateTime now) => new
    {
        merged.MusicBrainzID,
        merged.ReleaseGroupMusicBrainzID,
        merged.AudioDbID,
        merged.AudioDbArtistID,
        merged.AllMusicID,
        merged.DiscogsID,
        merged.MusicMozID,
        merged.LyricWikiID,
        merged.GeniusID,
        merged.WikipediaID,
        merged.WikidataID,
        merged.AmazonID,
        merged.Wikipedia,
        merged.Biography,
        merged.LastFmUrl,
        merged.Label,
        merged.Sales,
        merged.ReleaseFormat,
        merged.MusicBrainzReleaseType,
        merged.MusicBrainzReleaseCountry,
        merged.Year,
        merged.GenreId,
        merged.ReleaseDate,
        merged.IsLock,
        now,
        targetId,
    };

    private static CommandDefinition Command(string sql, object parameters, IDbTransaction transaction, CancellationToken cancellationToken) =>
        new(sql, parameters, transaction, cancellationToken: cancellationToken);

    private sealed record AlbumOwner(long? ArtistId, long? GenreId);
}