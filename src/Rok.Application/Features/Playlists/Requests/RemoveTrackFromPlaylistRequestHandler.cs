using System.Transactions;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Playlists.Requests;

public class RemoveTrackFromPlaylistRequest : IRequest<Result>
{
    public long PlaylistId { get; set; }

    public long TrackId { get; set; }
}


public class RemoveTrackFromPlaylistRequestHandler(IPlaylistTrackRepository _repository, IPlaylistHeaderRepository _playlistHeaderRepository, ITrackRepository _trackRepository, ILogger<RemoveTrackFromPlaylistRequestHandler> _logger) : IRequestHandler<RemoveTrackFromPlaylistRequest, Result>
{
    public async Task<Result> Handle(RemoveTrackFromPlaylistRequest message, CancellationToken cancellationToken)
    {
        TrackEntity? track = await _trackRepository.GetByIdAsync(message.TrackId);
        if (track == null)
            return Result.Fail(NotFoundError.ForEntity("Track", message.TrackId));

        PlaylistHeaderEntity? playlistHeader = await _playlistHeaderRepository.GetByIdAsync(message.PlaylistId);
        if (playlistHeader == null)
            return Result.Fail(NotFoundError.ForEntity("Playlist", message.PlaylistId));


        long id = await _repository.GetAsync(message.PlaylistId, message.TrackId);

        if (id <= 0)
            return Result.Fail(new OperationError("playlist.track_not_in_playlist", "Track not in the playlist."));

        using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            await RemoveTrackFromPlaylistAsync(message.PlaylistId, message.TrackId);
            await UpdatePlaylistHeaderAsync(playlistHeader, track);

            scope.Complete();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove track {TrackId} from playlist {PlaylistId}.", message.TrackId, message.PlaylistId);
            return Result.Fail(new OperationError("playlist.remove_track_failed", "Failed to remove track from playlist due to an error."));
        }

        return Result.Ok();
    }


    private Task RemoveTrackFromPlaylistAsync(long playlistId, long trackId)
    {
        return _repository.DeleteAsync(playlistId, trackId);
    }


    private Task UpdatePlaylistHeaderAsync(PlaylistHeaderEntity playlistHeader, TrackEntity track)
    {
        long trackDuration = Math.Max(0L, track.Duration);

        playlistHeader.Duration = Math.Max(0L, playlistHeader.Duration - trackDuration);
        playlistHeader.TrackCount = Math.Max(0, playlistHeader.TrackCount - 1);

        playlistHeader.EditDate = DateTime.UtcNow;

        return _playlistHeaderRepository.UpdateAsync(playlistHeader);
    }
}