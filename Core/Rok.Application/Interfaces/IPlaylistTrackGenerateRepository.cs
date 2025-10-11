namespace Rok.Application.Interfaces;

public interface IPlaylistTrackRepository
{
    Task<long> AddAsync(PlaylistTrackEntity entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<long> DeleteAsync(long playlistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<long> DeleteAsync(long playlistId, long trackId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<long> GetAsync(long playlistId, long trackId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}
