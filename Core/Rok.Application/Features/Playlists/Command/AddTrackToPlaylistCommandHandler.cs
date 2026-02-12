using System.Transactions;
using Rok.Application.Interfaces;

namespace Rok.Application.Features.Playlists.Command;

public class AddTrackToPlaylistCommand : ICommand<Result<long>>
{
    public long PlaylistId { get; set; }

    public long TrackId { get; set; }
}


public class AddTrackToPlaylistCommandHandler(IPlaylistTrackRepository _repository, IPlaylistHeaderRepository _playlistHeaderRepository, ITrackRepository _trackRepository) : ICommandHandler<AddTrackToPlaylistCommand, Result<long>>
{
    public async Task<Result<long>> HandleAsync(AddTrackToPlaylistCommand message, CancellationToken cancellationToken)
    {
        TrackEntity? track = await _trackRepository.GetByIdAsync(message.TrackId);
        if (track == null)
            return Result<long>.Fail("Track not found.");

        PlaylistHeaderEntity? playlistHeader = await _playlistHeaderRepository.GetByIdAsync(message.PlaylistId);
        if (playlistHeader == null)
            return Result<long>.Fail("Playlist not found.");


        long id = await _repository.GetAsync(message.PlaylistId, message.TrackId);

        if (id > 0)
            return Result<long>.Fail("Track already exists in the playlist.", "DUPLICATE");

        using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            id = await AddTrackToPlaylistAsync(message.PlaylistId, message.TrackId);
            await UpdatePlaylistHeaderAsync(playlistHeader, track);

            scope.Complete();
        }
        catch (Exception)
        {
            scope.Dispose();
            return Result<long>.Fail("Failed to add track to playlist due to an error.");
        }


        if (id > 0)
            return Result<long>.Success(id);
        else
            return Result<long>.Fail("Failed to add track to playlist.");
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
