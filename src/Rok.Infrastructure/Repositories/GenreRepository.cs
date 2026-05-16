using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

public class GenreRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<GenreRepository> logger, TimeProvider timeProvider) : GenericRepository<GenreEntity>(connection, backgroundConnection, null, logger, timeProvider), IGenreRepository
{
    private const string UpdateFavoriteSql = "UPDATE genres SET isFavorite = @isFavorite WHERE Id = @id";
    private const string UpdateLastListenSql = "UPDATE genres SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";
    private const string ResetListenCountSql = "UPDATE genres SET listenCount = 0";
    private const string UpdateStatisticsSql = "UPDATE genres SET trackCount = @trackCount, artistCount = @artistCount, albumCount = @albumCount, bestOfCount = @bestOfCount, liveCount = @liveCount, compilationCount = @compilationCount, totalDurationSeconds = @totalDurationSeconds WHERE id = @id";
    private const string DeleteOrphansSql = "DELETE FROM genres WHERE id NOT IN (SELECT DISTINCT genreId FROM tracks WHERE genreId IS NOT NULL)";


    public Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return ExecuteUpdateAsync(UpdateFavoriteSql, new { isFavorite, id }, kind);
    }

    public Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return ExecuteUpdateAsync(UpdateLastListenSql, new { lastListen = _timeProvider.GetUtcNow().UtcDateTime, id }, kind);
    }

    public Task<bool> ResetListenCountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        return ExecuteUpdateAsync(ResetListenCountSql, kind);
    }

    public Task<int> DeleteOrphansAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        return ExecuteNonQueryAsync(DeleteOrphansSql);
    }

    public Task<bool> UpdateStatisticsAsync(long id, int trackCount, int artistCount, int albumCount, int bestOfCount, int liveCount, int compilationCount, long totalDurationSeconds, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return ExecuteUpdateAsync(UpdateStatisticsSql, new { trackCount, artistCount, albumCount, bestOfCount, liveCount, compilationCount, totalDurationSeconds, id });
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