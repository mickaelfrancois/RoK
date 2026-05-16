using System.Transactions;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Playlists.Requests;

public class AddTrackToPlaylistRequest : IRequest<Result<long>>
{
    public long PlaylistId { get; set; }

    public long TrackId { get; set; }
}


public class AddTrackToPlaylistRequestHandler(IPlaylistTrackRepository _repository, IPlaylistHeaderRepository _playlistHeaderRepository, ITrackRepository _trackRepository, ILogger<AddTrackToPlaylistRequestHandler> _logger) : IRequestHandler<AddTrackToPlaylistRequest, Result<long>>
{
    public async Task<Result<long>> Handle(AddTrackToPlaylistRequest message, CancellationToken cancellationToken)
    {
        TrackEntity? track = await _trackRepository.GetByIdAsync(message.TrackId);
        if (track == null)
            return Result<long>.Fail(NotFoundError.ForEntity("Track", message.TrackId));

        PlaylistHeaderEntity? playlistHeader = await _playlistHeaderRepository.GetByIdAsync(message.PlaylistId);
        if (playlistHeader == null)
            return Result<long>.Fail(NotFoundError.ForEntity("Playlist", message.PlaylistId));


        long id = await _repository.GetAsync(message.PlaylistId, message.TrackId);

        if (id > 0)
            return Result<long>.Fail(new ConflictError("playlist.duplicate_track", "Track already exists in the playlist."));

        using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            id = await AddTrackToPlaylistAsync(message.PlaylistId, message.TrackId);
            await UpdatePlaylistHeaderAsync(playlistHeader, track);

            scope.Complete();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add track {TrackId} to playlist {PlaylistId}.", message.TrackId, message.PlaylistId);
            return Result<long>.Fail(new OperationError("playlist.add_track_transaction_failed", "Failed to add track to playlist due to an error."));
        }


        if (id > 0)
            return Result<long>.Ok(id);
        else
            return Result<long>.Fail(new OperationError("playlist.add_track_persistence_failed", "Failed to add track to playlist."));
    }


    private Task<long> AddTrackToPlaylistAsync(long playlistId, long trackId)
    {
        PlaylistTrackEntity playlistTrackEntity = new()
        {
            PlaylistId = playlistId,
            TrackId = trackId
        };

        return _repository.AddAsync(playlistTrackEntity);
    }


    private Task UpdatePlaylistHeaderAsync(PlaylistHeaderEntity playlistHeader, TrackEntity track)
    {
        playlistHeader.Duration += track.Duration;
        playlistHeader.TrackCount += 1;
        playlistHeader.EditDate = DateTime.UtcNow;

        return _playlistHeaderRepository.UpdateAsync(playlistHeader);
    }
}