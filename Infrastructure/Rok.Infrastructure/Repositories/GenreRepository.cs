using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Repositories;

public class GenreRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<GenreRepository> logger) : GenericRepository<GenreEntity>(connection, backgroundConnection, null, logger), IGenreRepository
{
    private const string UpdateFavoriteSql = "UPDATE genres SET isFavorite = @isFavorite WHERE Id = @id";
    private const string UpdateLastListenSql = "UPDATE albums SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";
    private const string UpdateStatisticsSql = "UPDATE genres SET trackCount = @trackCount, artistCount = @artistCount, albumCount = @albumCount, bestOfCount = @bestOfCount, liveCount = @liveCount, compilationCount = @compilationCount, totalDurationSeconds = @totalDurationSeconds WHERE id = @id";
    private const string DeleteOrphansSql = "DELETE FROM genres WHERE id NOT IN (SELECT DISTINCT genreId FROM tracks WHERE genreId IS NOT NULL)";


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

    public async Task<int> DeleteOrphansAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        return await ExecuteNonQueryAsync(DeleteOrphansSql);
    }

    public async Task<bool> UpdateStatisticsAsync(long id, int trackCount, int artistCount, int albumCount, int bestOfCount, int liveCount, int compilationCount, long totalDurationSeconds, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateStatisticsSql, new { trackCount, artistCount, albumCount, bestOfCount, liveCount, compilationCount, totalDurationSeconds, id });
    }


    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT genres.*                    
                     FROM genres                      
                """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE genres.{whereParam} = @{whereParam}";

        return query;
    }
}
