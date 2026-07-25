using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Requests;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Playlist.Services;

public class PlaylistUpdateService(
    IMediator mediator,
    IMessenger messenger,
    PlaylistPictureService pictureService,
    ILogger<PlaylistUpdateService> logger)
{
    public async Task<bool> SavePlaylistAsync(
        PlaylistHeaderDto playlist,
        IEnumerable<TrackViewModel> tracks,
        bool forceUpdate = false,
        List<PlaylistGroupDto>? groups = null)
    {
        TrackViewModel? track = tracks.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.ArtistName) && pictureService.PictureExists(c.ArtistName));

        UpdatePlaylistRequest command = new()
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Type = playlist.Type,
            Duration = tracks.Sum(c => c.Track.Duration),
            TrackCount = tracks.Count(),
            TrackMaximum = playlist.TrackMaximum,
            DurationMaximum = playlist.DurationMaximum,
            Picture = track?.ArtistName!,
            Groups = groups ?? playlist.Groups,
            ShuffleOnPlay = playlist.ShuffleOnPlay,
            RefreshOnPlay = playlist.RefreshOnPlay
        };

        bool hasChanges = forceUpdate || await HasChangesAsync(command, playlist.Id);

        if (!hasChanges)
            return false;

        logger.LogInformation("Updating playlist statistics for {Name} (Id: {Id})", playlist.Name, playlist.Id);

        Result result = await mediator.Send(command);

        if (result.IsFailure)
        {
            logger.LogError("Failed to update playlist statistics for {Name} (Id: {Id}). Error: {Error}",
                playlist.Name, playlist.Id, result.Errors[0]);
            return false;
        }

        if (command.Picture != null)
            playlist.Picture = command.Picture;
        playlist.Name = command.Name;
        playlist.TrackCount = command.TrackCount;
        playlist.TrackMaximum = command.TrackMaximum;
        playlist.DurationMaximum = command.DurationMaximum;
        playlist.Groups = command.Groups;
        playlist.ShuffleOnPlay = command.ShuffleOnPlay;
        playlist.RefreshOnPlay = command.RefreshOnPlay;

        messenger.Send(new PlaylistUpdatedMessage(playlist.Id, ActionType.Update));

        return true;
    }

    private async Task<bool> HasChangesAsync(UpdatePlaylistRequest command, long playlistId)
    {
        Result<PlaylistHeaderDto> persistedResult = await mediator.Send(new GetPlaylistByIdRequest(playlistId));

        if (persistedResult.IsFailure)
            return true;

        return PlaylistHeaderChangeDetector.HasChanges(command, persistedResult.Value);
    }

    public async Task<bool> RemoveTrackAsync(long playlistId, long trackId)
    {
        Result result = await mediator.Send(
            new RemoveTrackFromPlaylistRequest { PlaylistId = playlistId, TrackId = trackId });

        if (result.IsSuccess)
        {
            messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Update));
            return true;
        }

        messenger.Send(new ShowNotificationMessage
        {
            Message = "Failed to remove track from playlist",
            Type = NotificationType.Error
        });
        return false;
    }

    public async Task<bool> DeletePlaylistAsync(long playlistId, string playlistName)
    {
        Result<bool> result = await mediator.Send(new DeletePlaylistRequest { Id = playlistId });

        if (result.IsSuccess)
        {
            messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Delete));
            return true;
        }

        logger.LogError("Failed to delete playlist: {Name}. Error: {Error}", playlistName, result.Errors[0]);
        return false;
    }

    public async Task<bool> SaveTracksPositionAsync(long playlistId, List<long> tracks)
    {
        Result<bool> result = await mediator.Send(new MovePlaylistTracksRequest { PlaylistId = playlistId, Tracks = tracks });
        if (result.IsSuccess)
        {
            messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Delete));
            return true;
        }

        logger.LogError("Failed to update playlist: {PlaylistId}. Error: {Error}", playlistId, result.Errors[0]);
        return false;
    }
}