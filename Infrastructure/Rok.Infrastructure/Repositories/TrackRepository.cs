using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Randomizer;

namespace Rok.Infrastructure.Repositories;

public class TrackRepository(IDbConnection db, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, ILogger<TrackRepository> logger) : GenericRepository<TrackEntity>(db, backgroundDb, null, logger), ITrackRepository
{
    private const string UpdateScoreSql = "UPDATE tracks SET score = @score WHERE Id = @id";
    private const string UpdateLastListenSql = "UPDATE tracks SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";
    private const string ResetListenCountSql = "UPDATE tracks SET listenCount = 0";
    private const string UpdateSkipCountSql = "UPDATE tracks SET skipCount = skipCount + 1, lastSkip = @lastSkip WHERE Id = @id";
    private const string UpdateFileDateSql = "UPDATE tracks SET fileDate = @fileDate WHERE id = @id";
    private const string UpdateGetLyricsLastAttemptSql = "UPDATE tracks SET getLyricsLastAttempt = @lastAttemptDate WHERE id = @id";
    private const string DefaultGroupBy = " GROUP BY tracks.id";


    public async Task<IEnumerable<TrackEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        if (string.IsNullOrWhiteSpace(name))
            return [];

        name = $"%{name}%";
        string sql = GetSelectQuery() + $" WHERE tracks.title LIKE @name " + DefaultGroupBy;

        return await ExecuteQueryAsync(sql, kind, new { name });
    }

    public async Task<IEnumerable<TrackEntity>> GetByPlaylistIdAsync(long playlistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playlistId);

        string sql = GetSelectQuery() +
                     "INNER JOIN playlisttracks AS pt ON pt.trackId = tracks.id AND pt.playlistId = @playlistId " +
                     DefaultGroupBy;

        return await ExecuteQueryAsync(sql, kind, new { playlistId });
    }

    public new async Task<TrackEntity?> GetByNameAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery("title") + DefaultGroupBy;
        return await QuerySingleOrDefaultAsync(sql, kind, new { title = name });
    }


    public async Task<IEnumerable<TrackEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery() +
                     $"WHERE tracks.genreId = @genreId" + DefaultGroupBy;

        return await ExecuteQueryAsync(sql, kind, new { genreId });
    }

    public async Task<IEnumerable<TrackEntity>> GetByArtistIdAsync(long artistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery() +
                     "WHERE tracks.artistId = @artistId " +
                     DefaultGroupBy +
                     " ORDER BY albums.year DESC, tracks.trackNumber ASC";

        return await ExecuteQueryAsync(sql, kind, new { artistId });
    }

    public async Task<IEnumerable<TrackEntity>> GetByArtistIdAsync(IEnumerable<long> artistIds, int limit, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        if (artistIds == null)
            return Enumerable.Empty<TrackEntity>();

        List<long> idsList = artistIds.ToList();
        if (idsList.Count == 0)
            return Enumerable.Empty<TrackEntity>();

        int sampleCount = Math.Min(idsList.Count, Math.Max(limit * 5, 500));
        List<long> sampledArtistsIds = SamplingHelper.SamplePartialFisherYates(idsList, sampleCount);

        string sql = GetSelectQuery() +
                     "WHERE tracks.artistId IN @sampledArtistsIds " +
                     DefaultGroupBy +
                     "ORDER BY RANDOM() " +
                     "LIMIT @limit";

        return await ExecuteQueryAsync(sql, kind, new { sampledArtistsIds, limit });
    }

    public async Task<IEnumerable<TrackEntity>> GetByAlbumIdAsync(long albumId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(albumId);

        string sql = GetSelectQuery() +
                     "WHERE tracks.albumId = @albumId " + DefaultGroupBy;

        return await ExecuteQueryAsync(sql, kind, new { albumId });
    }

    public async Task<IEnumerable<TrackEntity>> GetByAlbumIdAsync(IEnumerable<long> albumIds, int limit, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        if (albumIds == null)
            return Enumerable.Empty<TrackEntity>();

        List<long> idsList = albumIds.ToList();
        if (idsList.Count == 0)
            return Enumerable.Empty<TrackEntity>();

        int sampleCount = Math.Min(idsList.Count, Math.Max(limit * 5, 500));
        List<long> sampledAlbumIds = SamplingHelper.SamplePartialFisherYates(idsList, sampleCount);

        string sql = GetSelectQuery() +
                     "WHERE tracks.albumId IN @sampledAlbumIds " +
                     DefaultGroupBy +
                     "ORDER BY RANDOM() " +
                     "LIMIT @limit";

        return await ExecuteQueryAsync(sql, kind, new { sampledAlbumIds, limit });
    }

    public async Task<bool> UpdateScoreAsync(long id, int score, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateScoreSql, new { score, id }, kind);
    }

    public async Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateLastListenSql, new { lastListen = DateTime.UtcNow, id }, kind);
    }

    public async Task<bool> ResetListenCountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        return await ExecuteUpdateAsync(ResetListenCountSql, kind);
    }

    public async Task<bool> UpdateSkipCountAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateSkipCountSql, new { lastSkip = DateTime.UtcNow, id }, kind);
    }


    public async Task<bool> UpdateFileDateAsync(long id, DateTime fileDate, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateFileDateSql, new { fileDate, id }, kind);
    }

    public async Task<bool> UpdateGetLyricsLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateGetLyricsLastAttemptSql, new { lastAttemptDate = DateTime.UtcNow, id }, kind);
    }


    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT tracks.*,
                     albums.name AS albumName, albums.isFavorite AS isAlbumFavorite, albums.isCompilation AS isAlbumCompilation, albums.isLive AS isAlbumLive,
                     artists.name AS artistName, artists.isFavorite AS isArtistFavorite, 
                     genres.name AS genreName, genres.isFavorite AS isGenreFavorite, 
                     countries.code AS countryCode, countries.english AS countryName,
                     GROUP_CONCAT(DISTINCT tags.Name) AS TagsAsString
                     FROM tracks 
                     LEFT JOIN artists ON artists.Id = tracks.artistId 
                     LEFT JOIN albums ON albums.Id = tracks.albumId 
                     LEFT JOIN genres ON genres.Id = tracks.genreId 
                     LEFT JOIN countries ON countries.Id = artists.countryId 
                     LEFT JOIN trackTags ON tracks.id = trackTags.trackId
                     LEFT JOIN tags ON trackTags.tagId = tags.id 
                """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE tracks.{whereParam} = @{whereParam}";

        return query;
    }

    public override string GetDefaultGroupBy()
    {
        return DefaultGroupBy;
    }


    public override string GetTableName()
    {
        return "tracks";
    }
}
