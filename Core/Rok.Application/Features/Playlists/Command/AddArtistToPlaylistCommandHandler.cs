using System.Transactions;
using Rok.Application.Interfaces;

namespace Rok.Application.Features.Playlists.Command;

public class AddArtistToPlaylistCommand : ICommand<Result<long>>
{
    public long PlaylistId { get; set; }

    public long ArtistId { get; set; }
}


public class AddArtistToPlaylistCommandHandler(IPlaylistTrackRepository _repository, IPlaylistHeaderRepository _playlistHeaderRepository, ITrackRepository _trackRepository) : ICommandHandler<AddArtistToPlaylistCommand, Result<long>>
{
    public async Task<Result<long>> HandleAsync(AddArtistToPlaylistCommand message, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByArtistIdAsync(message.ArtistId);
        if (tracks == null)
            return Result<long>.Fail("Track not found.");

        PlaylistHeaderEntity? playlistHeader = await _playlistHeaderRepository.GetByIdAsync(message.PlaylistId);
        if (playlistHeader == null)
            return Result<long>.Fail("Playlist not found.");

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
        catch (Exception)
        {
            scope.Dispose();
            return Result<long>.Fail("Failed to add track to playlist due to an error.");
        }

        if (addCount > 0)
            return Result<long>.Success(addCount);
        else
            return Result<long>.Fail("Failed to add tracks to playlist.");
    }


    private async Task<long> AddTrackToPlaylistAsync(long playlistId, long trackId)
    {
        PlaylistTrackEntity playlistTrackEntity = new()
        {
            PlaylistId = playlistId,
            TrackId = trackId
        };

        return await _repository.AddAsync(playlistTrackEntity);
    }


    private async Task UpdatePlaylistHeaderAsync(PlaylistHeaderEntity playlistHeader, TrackEntity track)
    {
        playlistHeader.Duration += track.Duration;
        playlistHeader.TrackCount += 1;
        playlistHeader.EditDate = DateTime.UtcNow;

        await _playlistHeaderRepository.UpdateAsync(playlistHeader);
    }
}
