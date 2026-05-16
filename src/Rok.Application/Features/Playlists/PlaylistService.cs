using MiF.Guard;
using Rok.Application.Features.Playlists.Requests;

namespace Rok.Application.Features.Playlists;

public class PlaylistService(IMediator _mediator) : IPlaylistService
{
    private readonly IMediator _mediator = Guard.Against.Null(_mediator);

    public async Task<PlaylistTracksDto> GenerateAsync(PlaylistHeaderDto playlist)
    {
        Guard.Against.Null(playlist);

        List<PlaylistGroupDto> emptyGroups = [];
        PlaylistTracksDto result = new PlaylistTracksDto();
        int trackIndex = 1;

        Dictionary<PlaylistGroupDto, List<TrackDto>> groupTracks = await LoadDataForAllGroupsAsync(playlist);

        while (!IsPlaylistTrackCountReached(result, playlist) && !AllGroupEmpty(emptyGroups, playlist))
        {
            foreach (KeyValuePair<PlaylistGroupDto, List<TrackDto>> group in groupTracks)
            {
                if (IsPlaylistTrackCountReached(result, playlist)) break;
                if (AllGroupEmpty(emptyGroups, playlist)) break;
                if (emptyGroups.Contains(group.Key)) continue;

                HandleGroup(group.Key, group.Value, result, emptyGroups, playlist, ref trackIndex);
            }
        }

        return result;
    }


    private async Task<Dictionary<PlaylistGroupDto, List<TrackDto>>> LoadDataForAllGroupsAsync(PlaylistHeaderDto playlist)
    {
        Dictionary<PlaylistGroupDto, List<TrackDto>> groupTracks = new();

        foreach (PlaylistGroupDto group in playlist.Groups)
        {
            IEnumerable<TrackDto> tracksGroup = await _mediator.Send(new GeneratePlaylistTracksRequest() { PlaylistTrackCount = playlist.TrackMaximum, Group = group });
            groupTracks.Add(group, tracksGroup.ToList());
        }

        return groupTracks;
    }


    private static void HandleGroup(PlaylistGroupDto group, List<TrackDto> tracksGroup, PlaylistTracksDto result, List<PlaylistGroupDto> emptyGroups, PlaylistHeaderDto playlist, ref int trackIndex)
    {
        int trackAdded = 0;

        tracksGroup.RemoveAll(t => result.Tracks.Any(r => r.Id == t.Id));

        foreach (TrackDto track in tracksGroup)
        {
            if (trackAdded >= group.TrackCount)
                return;

            if (!result.Tracks.Any(c => c.Id == track.Id))
            {
                track.TrackNumber = trackIndex++;
                result.Tracks.Add(track);
                trackAdded++;

                if (IsPlaylistTrackCountReached(result, playlist))
                    return;
            }
        }

        if (trackAdded == 0)
            emptyGroups.Add(group);
    }


    private static bool AllGroupEmpty(List<PlaylistGroupDto> emptyGroups, PlaylistHeaderDto playlist)
    {
        return emptyGroups.Count == playlist.Groups.Count;
    }


    private static bool IsPlaylistTrackCountReached(PlaylistTracksDto result, PlaylistHeaderDto playlist)
    {
        return result.Tracks.Count >= playlist.TrackMaximum;
    }
}