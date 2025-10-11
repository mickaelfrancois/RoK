using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Repositories;

public class GenreRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<GenreRepository> logger) : GenericRepository<GenreEntity>(connection, backgroundConnection, null, logger), IGenreRepository
{
    public async Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"UPDATE genres SET isFavorite = @isFavorite WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { isFavorite, id });

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"UPDATE genres SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { lastListen = DateTime.UtcNow, id });

        return rowsAffected > 0;
    }

    public async Task<int> DeleteGenresWithoutTracks(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "DELETE FROM genres WHERE id NOT IN (SELECT DISTINCT genreId FROM tracks WHERE genreId IS NOT NULL)";

        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(sql);
    }

    public async Task<bool> UpdateStatisticsAsync(long id, int trackCount, int artistCount, int albumCount, int bestOfCount, int liveCount, int compilationCount, long totalDurationSeconds, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE genres SET trackCount = @trackCount, artistCount = @artistCount, albumCount = @albumCount, bestOfCount = @bestOfCount, liveCount = @liveCount, compilationCount = @compilationCount, totalDurationSeconds = @totalDurationSeconds WHERE id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { trackCount, artistCount, albumCount, bestOfCount, liveCount, compilationCount, totalDurationSeconds, id });

        return rowsAffected > 0;
    }


    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT genres.*                    
                     FROM genres                      
                """;

        if (string.IsNullOrEmpty(whereParam) == false)
            query += $" WHERE genres.{whereParam} = @{whereParam}";

        return query;
    }
}
