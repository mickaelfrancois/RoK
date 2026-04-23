using Rok.Application.Features.Playlists.Command;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Playlist.Services;

public class PlaylistUpdateService(
    IMediator mediator,
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

        UpdatePlaylistCommand command = new()
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Type = playlist.Type,
            Duration = tracks.Sum(c => c.Track.Duration),
            TrackCount = tracks.Count(),
            TrackMaximum = playlist.TrackMaximum,
            DurationMaximum = playlist.DurationMaximum,
            Picture = track?.ArtistName!,
            Groups = groups ?? playlist.Groups
        };

        bool hasChanges = forceUpdate ||
            command.Name != playlist.Name ||
            command.Duration != playlist.Duration ||
            command.TrackCount != playlist.TrackCount ||
            command.TrackMaximum != playlist.TrackMaximum ||
            command.DurationMaximum != playlist.DurationMaximum ||
            command.Picture != playlist.Picture ||
            command.Groups != playlist.Groups;

        if (!hasChanges)
            return false;

        logger.LogInformation("Updating playlist statistics for {Name} (Id: {Id})", playlist.Name, playlist.Id);

        Result result = await mediator.SendMessageAsync(command);

        if (result.IsError)
        {
            logger.LogError("Failed to update playlist statistics for {Name} (Id: {Id}). Error: {Error}",
                playlist.Name, playlist.Id, result.Error);
            return false;
        }

        if (command.Picture != null)
            playlist.Picture = command.Picture;
        playlist.Name = command.Name;
        playlist.TrackCount = command.TrackCount;
        playlist.TrackMaximum = command.TrackMaximum;
        playlist.DurationMaximum = command.DurationMaximum;
        playlist.Groups = command.Groups;

        Messenger.Send(new PlaylistUpdatedMessage(playlist.Id, ActionType.Update));

        return true;
    }

    public async Task<bool> RemoveTrackAsync(long playlistId, long trackId)
    {
        Result result = await mediator.SendMessageAsync(
            new RemoveTrackFromPlaylistCommand { PlaylistId = playlistId, TrackId = trackId });

        if (result.IsSuccess)
        {
            Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Update));
            return true;
        }

        Messenger.Send(new ShowNotificationMessage
        {
            Message = "Failed to remove track from playlist",
            Type = NotificationType.Error
        });
        return false;
    }

    public async Task<bool> DeletePlaylistAsync(long playlistId, string playlistName)
    {
        Result<bool> result = await mediator.SendMessageAsync(new DeletePlaylistCommand { Id = playlistId });

        if (result.IsSuccess)
        {
            Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Delete));
            return true;
        }

        logger.LogError("Failed to delete playlist: {Name}. Error: {Error}", playlistName, result.Error);
        return false;
    }

    public async Task<bool> SaveTracksPositionAsync(long playlistId, List<long> tracks)
    {
        Result<bool> result = await mediator.SendMessageAsync(new MovePlaylistTracksCommand { PlaylistId = playlistId, Tracks = tracks });
        if (result.IsSuccess)
        {
            Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Delete));
            return true;
        }

        logger.LogError("Failed to update playlist: {PlaylistId}. Error: {Error}", playlistId, result.Error);
        return false;
    }
}