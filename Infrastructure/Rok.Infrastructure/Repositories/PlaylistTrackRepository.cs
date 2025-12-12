using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Repositories;

public class PlaylistTrackRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<PlaylistTrackRepository> logger) : GenericRepository<PlaylistTrackRepository>(connection, backgroundConnection, null, logger), IPlaylistTrackRepository
{
    private const string AddSql = "INSERT INTO playlisttracks (playlistid, trackid, position, listened, creatdate) VALUES (@playlistId, @trackId, @position, @listened, @creatdate)";
    private const string DeleteSql = "DELETE FROM playlisttracks WHERE playlistid = @playlistId";
    private const string DeleteTrackSql = "DELETE FROM playlisttracks WHERE playlistid = @playlistId AND trackid = @trackId";
    private const string SelectSql = "SELECT id FROM playlisttracks WHERE playlistid = @playlistId AND trackid = @trackId";


    public async Task<long> AddAsync(PlaylistTrackEntity entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(AddSql, new { playlistId = entity.PlaylistId, trackId = entity.TrackId, position = entity.Position, listened = entity.Listened, creatdate = DateTime.UtcNow });
    }

    public async Task<long> DeleteAsync(long playlistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(DeleteSql, new { playlistId });
    }

    public async Task<long> DeleteAsync(long playlistId, long trackId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(DeleteTrackSql, new { playlistId, trackId });
    }

    public async Task<long> GetAsync(long playlistId, long trackId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteScalarAsync<int>(SelectSql, new { playlistId, trackId });
    }
}
