using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Repositories;

public class TrackRepository(IDbConnection db, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, ILogger<TrackRepository> logger) : GenericRepository<TrackEntity>(db, backgroundDb, null, logger), ITrackRepository
{
    public async Task<IEnumerable<TrackEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        name = $"%{name}%";
        string sql = GetSelectQuery() + " WHERE tracks.title LIKE @name";

        return await ExecuteQueryAsync(sql, kind, new { name });
    }

    public async Task<IEnumerable<TrackEntity>> GetByPlaylistIdAsync(long playlistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery() +
                     "INNER JOIN playlisttracks AS pt ON pt.trackId = tracks.id AND pt.playlistId = @playlistId";

        return await ExecuteQueryAsync(sql, kind, new { playlistId });
    }

    public async Task<IEnumerable<TrackEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery() +
                     "WHERE tracks.genreId = @genreId";

        return await ExecuteQueryAsync(sql, kind, new { genreId });
    }

    public async Task<IEnumerable<TrackEntity>> GetByArtistIdAsync(long artistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery() +
                     "WHERE tracks.artistId = @artistId" +
                     " ORDER BY albums.year DESC, tracks.trackNumber ASC";

        return await ExecuteQueryAsync(sql, kind, new { artistId });
    }

    public async Task<IEnumerable<TrackEntity>> GetByArtistIdAsync(IEnumerable<long> artistIds, int limit, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        if (artistIds == null || !artistIds.Any())
            return Enumerable.Empty<TrackEntity>();

        string sql = GetSelectQuery() +
                     "WHERE tracks.artistId IN @artistIds " +
                     "ORDER BY RANDOM() " +
                     "LIMIT @limit";

        return await ExecuteQueryAsync(sql, kind, new { artistIds, limit });
    }

    public async Task<IEnumerable<TrackEntity>> GetByAlbumIdAsync(long albumId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery() +
                     "WHERE tracks.albumId = @albumId";

        return await ExecuteQueryAsync(sql, kind, new { albumId });
    }

    public async Task<IEnumerable<TrackEntity>> GetByAlbumIdAsync(IEnumerable<long> albumIds, int limit, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        if (albumIds == null || !albumIds.Any())
            return Enumerable.Empty<TrackEntity>();

        string sql = GetSelectQuery() +
                     "WHERE tracks.albumId IN @albumIds " +
                     "ORDER BY RANDOM() " +
                     "LIMIT @limit";

        return await ExecuteQueryAsync(sql, kind, new { albumIds, limit });
    }

    public async Task<bool> UpdateScoreAsync(long id, int score, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE tracks SET score = @score WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { score, id });

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE tracks SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { lastListen = DateTime.UtcNow, id });

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateSkipCountAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE tracks SET skipCount = skipCount + 1, lastSkip = @lastSkip WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { lastSkip = DateTime.UtcNow, id });

        return rowsAffected > 0;
    }


    public async Task<bool> UpdateFileDateAsync(long id, DateTime fileDate, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE tracks SET fileDate = @fileDate WHERE id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { fileDate, id });

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateGetLyricsLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE tracks SET getLyricsLastAttempt = @lastAttemptDate WHERE id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { lastAttemptDate = DateTime.UtcNow, id });

        return rowsAffected > 0;
    }

    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT tracks.*,
                     albums.name AS albumName, albums.isFavorite AS isAlbumFavorite, albums.isCompilation AS isAlbumCompilation, 
                     artists.name AS artistName, artists.isFavorite AS isArtistFavorite, 
                     genres.name AS genreName, genres.isFavorite AS isGenreFavorite, 
                     countries.code AS countryCode, countries.english AS countryName
                     FROM tracks 
                     LEFT JOIN artists ON artists.Id = tracks.artistId 
                     LEFT JOIN albums ON albums.Id = tracks.albumId 
                     LEFT JOIN genres ON genres.Id = tracks.genreId 
                     LEFT JOIN countries ON countries.Id = artists.countryId 
                """;

        return query;
    }

    public override string GetTableName()
    {
        return "tracks";
    }
}
