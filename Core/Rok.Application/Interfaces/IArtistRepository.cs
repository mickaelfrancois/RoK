using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Interfaces;

public interface IArtistRepository : IRepository<ArtistEntity>
{
    Task<IEnumerable<IArtistEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<IArtistEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> PatchAsync(IUpdateArtistEntity entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> ResetListenCountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<int> DeleteOrphansAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateStatisticsAsync(long id, int trackCount, long totalDurationSeconds, int albumCount, int bestOfCount, int liveCount, int compilationCount, int? yearMini, int? yearMaxi, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateGetMetaDataLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}
