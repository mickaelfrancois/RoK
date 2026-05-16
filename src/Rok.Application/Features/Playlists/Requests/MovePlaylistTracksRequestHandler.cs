using System.Transactions;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Playlists.Requests;

public class MovePlaylistTracksRequest : IRequest<Result<bool>>
{
    public long PlaylistId { get; set; }

    public List<long> Tracks { get; set; } = [];
}


public class MovePlaylistTracksRequestHandler(IPlaylistTrackRepository _repository, ILogger<MovePlaylistTracksRequestHandler> _logger) : IRequestHandler<MovePlaylistTracksRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(MovePlaylistTracksRequest message, CancellationToken cancellationToken)
    {
        using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            IEnumerable<PlaylistTrackEntity> currentTracksEnumerable = await _repository.GetAsync(message.PlaylistId);
            List<PlaylistTrackEntity> currentTracks = currentTracksEnumerable.ToList();

            Dictionary<long, PlaylistTrackEntity> byTrackId = currentTracks.ToDictionary(t => t.TrackId);

            for (int newIndex = 0; newIndex < message.Tracks.Count; newIndex++)
            {
                long trackId = message.Tracks[newIndex];

                if (!byTrackId.TryGetValue(trackId, out PlaylistTrackEntity? playlistTrack))
                    continue;

                if (playlistTrack.Position == newIndex)
                    continue;

                long rowsAffected = await _repository.UpdatePositionAsync(playlistTrack.Id, newIndex, RepositoryConnectionKind.Foreground);
                if (rowsAffected <= 0)
                    throw new InvalidOperationException("Failed to update track position in playlist.");
            }

            scope.Complete();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move tracks in playlist {PlaylistId}.", message.PlaylistId);
            return Result<bool>.Fail(new OperationError("playlist.move_tracks_failed", "Failed to move tracks in playlist due to an error."));
        }

        return Result<bool>.Ok(true);
    }
}