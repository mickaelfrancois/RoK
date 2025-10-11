using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Repositories;

public class PlaylistTrackRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<PlaylistTrackRepository> logger) : GenericRepository<PlaylistTrackRepository>(connection, backgroundConnection, null, logger), IPlaylistTrackRepository
{
    public async Task<long> AddAsync(PlaylistTrackEntity entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "INSERT INTO playlisttracks (playlistid, trackid, position, listened, creatdate) VALUES (@playlistId, @trackId, @position, @listened, @creatdate)";

        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(sql, new { playlistId = entity.PlaylistId, trackId = entity.TrackId, position = entity.Position, listened = entity.Listened, creatdate = DateTime.UtcNow });
    }

    public async Task<long> DeleteAsync(long playlistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "DELETE FROM playlisttracks WHERE playlistid = @playlistId";

        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(sql, new { playlistId });
    }

    public async Task<long> DeleteAsync(long playlistId, long trackId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "DELETE FROM playlisttracks WHERE playlistid = @playlistId AND trackid = @trackId";

        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(sql, new { playlistId, trackId });
    }

    public async Task<long> GetAsync(long playlistId, long trackId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "SELECT id FROM playlisttracks WHERE playlistid = @playlistId AND trackid = @trackId";

        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteScalarAsync<int>(sql, new { playlistId = playlistId, trackId = trackId });
    }
}
