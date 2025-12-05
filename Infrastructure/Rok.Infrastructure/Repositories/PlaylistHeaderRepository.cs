using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Repositories;

public class PlaylistHeaderRepository : GenericRepository<PlaylistHeaderEntity>, IPlaylistHeaderRepository
{
    public PlaylistHeaderRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<PlaylistHeaderRepository> logger) : base(connection, backgroundConnection, null, logger)
    {
    }


    public async Task<bool> UpdatePictureAsync(long id, string picture, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"UPDATE playlists SET picture = @picture WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { picture, id }, _transaction);

        return rowsAffected > 0;
    }

    public async Task<int> DeleteAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);

        if (localConnection.State == ConnectionState.Closed)
            localConnection.Open();

        using IDbTransaction transaction = localConnection.BeginTransaction();

        try
        {
            await localConnection.ExecuteAsync("DELETE FROM playlisttracks WHERE playlistid = @id", new { id });
            int affected = await localConnection.ExecuteAsync("DELETE FROM playlists WHERE id = @id", new { id });

            transaction.Commit();
            return affected;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                    SELECT id, name, picture, duration, trackCount, trackMaximum, durationMaximum, groupsJson, type, creatDate, editDate 
                    FROM playlists                                  
                """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE playlists.{whereParam} = @{whereParam}";

        return query;
    }
}
