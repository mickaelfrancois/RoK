using Rok.Application.Interfaces;
using System.Transactions;

namespace Rok.Application.Features.Playlists.Command;

public class RemoveTrackFromPlaylistCommand : ICommand<Result>
{
    public long PlaylistId { get; set; }

    public long TrackId { get; set; }
}


public class RemoveTrackFromPlaylistCommandHandler(IPlaylistTrackRepository _repository, IPlaylistHeaderRepository _playlistHeaderRepository, ITrackRepository _trackRepository) : ICommandHandler<RemoveTrackFromPlaylistCommand, Result>
{
    public async Task<Result> HandleAsync(RemoveTrackFromPlaylistCommand message, CancellationToken cancellationToken)
    {
        TrackEntity? track = await _trackRepository.GetByIdAsync(message.TrackId);
        if (track == null)
            return Result.Fail("Track not found.");

        PlaylistHeaderEntity? playlistHeader = await _playlistHeaderRepository.GetByIdAsync(message.PlaylistId);
        if (playlistHeader == null)
            return Result.Fail("Playlist not found.");


        long id = await _repository.GetAsync(message.PlaylistId, message.TrackId);

        if (id <= 0)
            return Result.Fail("Track not exists in the playlist.");

        using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            await RemoveTrackFromPlaylistAsync(message.PlaylistId, message.TrackId);
            await UpdatePlaylistHeaderAsync(playlistHeader, track);

            scope.Complete();
        }
        catch (Exception)
        {
            scope.Dispose();
            return Result.Fail("Failed to remove track to playlist due to an error.");
        }

        return Result.Success();
    }


    private async Task RemoveTrackFromPlaylistAsync(long playlistId, long trackId)
    {
        await _repository.DeleteAsync(playlistId, trackId);
    }


    private async Task UpdatePlaylistHeaderAsync(PlaylistHeaderEntity playlistHeader, TrackEntity track)
    {
        long trackDuration = Math.Max(0L, track.Duration);

        playlistHeader.Duration = Math.Max(0L, playlistHeader.Duration - trackDuration);
        playlistHeader.TrackCount = Math.Max(0, playlistHeader.TrackCount - 1);

        playlistHeader.EditDate = DateTime.UtcNow;

        await _playlistHeaderRepository.UpdateAsync(playlistHeader);
    }
}

