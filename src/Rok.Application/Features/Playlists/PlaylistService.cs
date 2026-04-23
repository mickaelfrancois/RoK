using MiF.Guard;
using Rok.Application.Features.Playlists.Query;

namespace Rok.Application.Features.Playlists;

public class PlaylistService(IMediator _mediator) : IPlaylistService
{
    private readonly IMediator _mediator = Guard.Against.Null(_mediator);

    private PlaylistHeaderDto? _currentPlaylist;

    private PlaylistTracksDto? _currentResult;

    private List<PlaylistGroupDto>? _emptyGroups;

    private int _trackIndex = 1;


    public async Task<PlaylistTracksDto> GenerateAsync(PlaylistHeaderDto playlist)
    {
        _currentPlaylist = Guard.Against.Null(playlist);

        _emptyGroups = [];
        _currentResult = new PlaylistTracksDto();
        List<TrackDto> tracks = [];
        _trackIndex = 1;

        Dictionary<PlaylistGroupDto, List<TrackDto>> groupTracks = await LoadDataForAllGroupsAsync(playlist);

        while (IsPlaylistTrackCountReached() == false && AllGroupEmpty() == false)
        {
            foreach (KeyValuePair<PlaylistGroupDto, List<TrackDto>> group in groupTracks)
            {
                if (IsPlaylistTrackCountReached()) break;
                if (AllGroupEmpty()) break;
                if (IsGroupEmpty(group.Key)) continue;

                HandleGroup(group.Key, group.Value);
            }
        }

        return _currentResult;
    }


    private async Task<Dictionary<PlaylistGroupDto, List<TrackDto>>> LoadDataForAllGroupsAsync(PlaylistHeaderDto playlist)
    {
        Dictionary<PlaylistGroupDto, List<TrackDto>> groupTracks = new();

        foreach (PlaylistGroupDto group in playlist.Groups)
        {
            IEnumerable<TrackDto> tracksGroup = await _mediator.SendMessageAsync(new GeneratePlaylistTracksQuery() { PlaylistTrackCount = playlist.TrackMaximum, Group = group });
            groupTracks.Add(group, tracksGroup.ToList());
        }

        return groupTracks;
    }


    private void HandleGroup(PlaylistGroupDto group, List<TrackDto> tracksGroup)
    {
        int trackAdded = 0;

        tracksGroup.RemoveAll(t => _currentResult!.Tracks.Any(r => r.Id == t.Id));

        foreach (TrackDto track in tracksGroup)
        {
            if (trackAdded >= group.TrackCount)
                return;

            if (!_currentResult!.Tracks.Any(c => c.Id == track.Id))
            {
                track.TrackNumber = _trackIndex++;
                _currentResult.Tracks.Add(track);
                trackAdded++;

                if (IsPlaylistTrackCountReached())
                    return;
            }
        }

        if (trackAdded == 0)
            _emptyGroups!.Add(group);
    }


    private bool IsGroupEmpty(PlaylistGroupDto group)
    {
        return _emptyGroups!.Contains(group);
    }


    private bool AllGroupEmpty()
    {
        return _emptyGroups!.Count == _currentPlaylist!.Groups.Count;
    }


    private bool IsPlaylistTrackCountReached()
    {
        return _currentResult!.Tracks.Count >= _currentPlaylist!.TrackMaximum;
    }
}
