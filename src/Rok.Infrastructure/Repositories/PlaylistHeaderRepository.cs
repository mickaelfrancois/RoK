using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

public class PlaylistHeaderRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<PlaylistHeaderRepository> logger, TimeProvider timeProvider) : GenericRepository<PlaylistHeaderEntity>(connection, backgroundConnection, null, logger, timeProvider), IPlaylistHeaderRepository
{
    private const string UpdatePictureSql = "UPDATE playlists SET picture = @picture WHERE Id = @id";
    private const string DeletePlaylistSql = "DELETE FROM playlists WHERE id = @id";
    private const string DeletePlaylistTracksSql = "DELETE FROM playlisttracks WHERE playlistid = @id";


    public Task<bool> UpdatePictureAsync(long id, string picture, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return ExecuteUpdateAsync(UpdatePictureSql, new { picture, id }, kind);
    }

    public async Task<int> DeleteAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);

        if (localConnection.State == ConnectionState.Closed)
            localConnection.Open();

        using IDbTransaction transaction = localConnection.BeginTransaction();

        try
        {
            await localConnection.ExecuteAsync(DeletePlaylistTracksSql, new { id });
            int affected = await localConnection.ExecuteAsync(DeletePlaylistSql, new { id });

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
