namespace Rok.Application.Interfaces;

public interface ITrackRepository : IRepository<TrackEntity>
{
    Task<IEnumerable<TrackEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<TrackEntity>> GetByPlaylistIdAsync(long playlistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<TrackEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<TrackEntity>> GetByAlbumIdAsync(long albumId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<TrackEntity>> GetByAlbumIdAsync(IEnumerable<long> albumIds, int limit, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<TrackEntity>> GetByArtistIdAsync(long artistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<TrackEntity>> GetByArtistIdAsync(IEnumerable<long> artistIds, int limit, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateScoreAsync(long id, int score, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateSkipCountAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateFileDateAsync(long id, DateTime fileDate, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateGetLyricsLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}
