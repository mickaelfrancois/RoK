using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Interfaces;

public interface IAlbumRepository : IRepository<AlbumEntity>
{
    Task<IEnumerable<IAlbumEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<IAlbumEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<IAlbumEntity>> GetByArtistIdAsync(long artistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> PatchAsync(IUpdateAlbumEntity entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> ResetListenCountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<int> DeleteOrphansAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateStatisticsAsync(long id, int trackCount, long duration, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateGetMetaDataLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}
