using System.Transactions;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Playlists.Requests;

public class AddArtistToPlaylistRequest : IRequest<Result<long>>
{
    public long PlaylistId { get; set; }

    public long ArtistId { get; set; }
}


public class AddArtistToPlaylistRequestHandler(IPlaylistTrackRepository _repository, IPlaylistHeaderRepository _playlistHeaderRepository, ITrackRepository _trackRepository, ILogger<AddArtistToPlaylistRequestHandler> _logger) : IRequestHandler<AddArtistToPlaylistRequest, Result<long>>
{
    public async Task<Result<long>> Handle(AddArtistToPlaylistRequest message, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByArtistIdAsync(message.ArtistId);
        if (tracks == null)
            return Result<long>.Fail(NotFoundError.ForEntity("Track", message.ArtistId));

        PlaylistHeaderEntity? playlistHeader = await _playlistHeaderRepository.GetByIdAsync(message.PlaylistId);
        if (playlistHeader == null)
            return Result<long>.Fail(NotFoundError.ForEntity("Playlist", message.PlaylistId));

        int addCount = 0;

        using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            foreach (TrackEntity track in tracks)
            {
                long existingId = await _repository.GetAsync(message.PlaylistId, track.Id);
                if (existingId <= 0)
                {
                    await AddTrackToPlaylistAsync(message.PlaylistId, track.Id);
                    await UpdatePlaylistHeaderAsync(playlistHeader, track);

                    addCount++;
                }
            }

            scope.Complete();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add artist {ArtistId} tracks to playlist {PlaylistId}.", message.ArtistId, message.PlaylistId);
            return Result<long>.Fail(new OperationError("playlist.add_artist_transaction_failed", "Failed to add track to playlist due to an error."));
        }

        if (addCount > 0)
            return Result<long>.Ok(addCount);
        else
            return Result<long>.Fail(new OperationError("playlist.add_artist_no_tracks", "Failed to add tracks to playlist."));
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