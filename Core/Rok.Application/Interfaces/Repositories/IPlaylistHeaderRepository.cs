namespace Rok.Application.Interfaces.Repositories;

public interface IPlaylistHeaderRepository : IRepository<PlaylistHeaderEntity>
{
    Task<bool> UpdatePictureAsync(long id, string picture, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<int> DeleteAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}
