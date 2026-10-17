using Rok.Application.Tag;

﻿namespace Rok.Application.Interfaces.Repositories;

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

    Task<bool> ResetListenCountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateSkipCountAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateFileDateAsync(long id, DateTime fileDate, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    /// <summary>
    /// Updates the title, track number, disc, composers, BPM, file date and file size of a track, and stamps its edit date, without touching score, listen counts or playlist links. Empty composers are stored as NULL.
    /// </summary>
    Task<bool> UpdateTagFieldsAndFileInfoAsync(long id, TagFieldValues values, DateTime fileDate, long size, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    /// <summary>
    /// Updates only the file date and the file size of a track. Used after a tag write that changes no database column of the track.
    /// </summary>
    Task<bool> UpdateFileInfoAsync(long id, DateTime fileDate, long size, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateGetLyricsLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<TrackEntity?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Gets the tracks whose file does not carry yet the tag fields held by the database. Rows of deleted tracks are ignored.</summary>
    Task<IEnumerable<PendingTagWrite>> GetPendingTagWritesAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}