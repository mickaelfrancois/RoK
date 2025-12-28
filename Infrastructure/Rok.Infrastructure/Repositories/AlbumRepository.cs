using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;
using Rok.Shared;

namespace Rok.Infrastructure.Repositories;

public class AlbumRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<AlbumRepository> logger) : GenericRepository<AlbumEntity>(connection, backgroundConnection, null, logger), IAlbumRepository
{
    private const string UpdateFavoriteSql = "UPDATE albums SET isFavorite = @isFavorite WHERE Id = @id";
    private const string UpdateLastListenSql = "UPDATE albums SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";
    private const string ResetListenCountSql = "UPDATE albums SET listenCount = 0";
    private const string UpdateStatisticsSql = "UPDATE albums SET trackCount = @trackCount, duration = @duration WHERE id = @id";
    private const string UpdateMetadataAttemptSql = "UPDATE albums SET getMetaDataLastAttempt = @lastAttemptDate WHERE id = @id";
    private const string DeleteOrphansSql = "DELETE FROM albums WHERE id NOT IN (SELECT DISTINCT albumId FROM tracks WHERE albumId IS NOT NULL)";


    public async Task<IEnumerable<IAlbumEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        if (string.IsNullOrWhiteSpace(name))
            return [];

        name = $"%{name}%";
        string sql = GetSelectQuery() + " WHERE albums.name LIKE @name";

        return await ExecuteQueryAsync(sql, kind, new { name });
    }

    public async Task<IEnumerable<IAlbumEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(genreId);

        string sql = GetSelectQuery("genreId");

        return await ExecuteQueryAsync(sql, kind, new { genreId });
    }

    public async Task<IEnumerable<IAlbumEntity>> GetByArtistIdAsync(long artistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(artistId);

        string sql = GetSelectQuery("artistId") + " ORDER BY albums.year DESC";

        return await ExecuteQueryAsync(sql, kind, new { artistId });
    }


    public async Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateFavoriteSql, new { isFavorite, id }, kind);
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

    public async Task<bool> UpdateStatisticsAsync(long id, int trackCount, long duration, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateStatisticsSql, new { trackCount, duration, id });
    }

    public async Task<bool> UpdateGetMetaDataLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateMetadataAttemptSql, new { lastAttemptDate = DateTime.UtcNow, id });
    }

    public async Task<int> DeleteOrphansAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        return await ExecuteNonQueryAsync(DeleteOrphansSql);
    }


    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT albums.*,
                     genres.name AS genreName, genres.isFavorite AS isGenreFavorite,
                     artists.name AS artistName, artists.isFavorite AS isArtistFavorite,
                     countries.code AS countryCode, countries.english AS countryName 
                     FROM albums 
                     LEFT JOIN genres ON genres.Id = albums.genreId                      
                     LEFT JOIN artists ON artists.Id = albums.artistId 
                     LEFT JOIN countries ON countries.Id = artists.countryId
                """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE albums.{whereParam} = @{whereParam}";

        return query;
    }


    public override string GetTableName()
    {
        return "albums";
    }


    private static void ApplyPatch<T>(PatchField<T>? wrapper, Action<T?> setter)
    {
        if (wrapper?.IsSet == true)
        {
            setter(wrapper.Value);
        }
    }
}
