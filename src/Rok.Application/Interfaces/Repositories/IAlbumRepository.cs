using Rok.Domain.Enums;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Interfaces.Repositories;

public interface IAlbumRepository : IRepository<AlbumEntity>
{
    Task<IEnumerable<IAlbumEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<IAlbumEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<IAlbumEntity>> GetByArtistIdAsync(long artistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdatePictureDominantColorAsync(long id, long? colorValue, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> ResetListenCountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<int> DeleteOrphansAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateStatisticsAsync(long id, int trackCount, long duration, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateGetMetaDataLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    /// <summary>
    /// Sets the compilation flag and the owning artist of an album, leaving every other column untouched.
    /// </summary>
    /// <param name="id">Album id.</param>
    /// <param name="isCompilation">New compilation flag.</param>
    /// <param name="artistId">New owning artist id, or <c>null</c> for a compilation without artist.</param>
    /// <param name="source">Origin of the compilation status.</param>
    /// <param name="kind">Connection to use.</param>
    /// <returns><c>true</c> when a row was updated.</returns>
    Task<bool> UpdateCompilationAsync(long id, bool isCompilation, long? artistId, ECompilationSource source, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}