namespace Rok.Application.Interfaces;

public interface IGenreRepository : IRepository<GenreEntity>
{
    Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> ResetListenCountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<int> DeleteOrphansAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateStatisticsAsync(long id, int trackCount, int artistCount, int albumCount, int bestOfCount, int liveCount, int compilationCount, long totalDurationSeconds, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}
