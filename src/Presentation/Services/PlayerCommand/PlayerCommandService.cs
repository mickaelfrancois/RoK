using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Player;
using Rok.Application.Randomizer;

namespace Rok.Services.PlayerCommand;

public sealed class PlayerCommandService(IPlayerService playerService, IMediator mediator) : IPlayerCommandService
{
    private static readonly int MaxTracks = 100;

    public void Play() => playerService.Play();

    public void Pause() => playerService.Pause();

    public void Next() => playerService.Skip();

    public void Previous() => playerService.Previous();

    public void ToggleMute() => playerService.IsMuted = !playerService.IsMuted;

    public void SetVolume(double volume) => playerService.Volume = Math.Clamp(volume, 0, 100);

    public void Shuffle() => playerService.ShuffleTracks();

    public void ToggleLoop() => playerService.IsLoopingEnabled = !playerService.IsLoopingEnabled;


    public void Seek(double positionSeconds)
    {
        if (!playerService.CanSeek)
            return;

        playerService.Position = Math.Max(0, positionSeconds);
    }


    public bool PlayQueuedTrack(long trackId)
    {
        TrackDto? track = playerService.Playlist.Find(t => t.Id == trackId);

        if (track is null)
            return false;

        playerService.Start(track);
        return true;
    }


    public async Task<bool> SetScoreAsync(long trackId, int score)
    {
        int clamped = Math.Clamp(score, 0, 5);

        Result<bool> result = await mediator.Send(new UpdateScoreRequest(trackId, clamped));

        if (!result.IsSuccess)
            return false;

        foreach (TrackDto track in playerService.Playlist.Where(t => t.Id == trackId))
            track.Score = clamped;

        return true;
    }


    public async Task<bool> ListenPlaylistByIdAsync(long playlistId)
    {
        IEnumerable<TrackDto> tracks = await mediator.Send(new GetTracksByPlaylistIdRequest(playlistId));
        List<TrackDto> list = tracks.ToList();

        if (list.Count == 0)
            return false;

        playerService.LoadPlaylist(list);
        playerService.Play();
        return true;
    }


    public void Toggle()
    {
        if (playerService.PlaybackState == EPlaybackState.Playing)
            playerService.Pause();
        else
            playerService.Play();
    }


    public async Task<bool> ListenPlaylistAsync(string playlistName)
    {
        IEnumerable<PlaylistHeaderDto> playlists = await mediator.Send(new GetAllPlaylistsRequest());
        PlaylistHeaderDto? playlist = playlists.FirstOrDefault(p => p.Name.Equals(playlistName, StringComparison.OrdinalIgnoreCase));

        if (playlist is null)
            return false;

        IEnumerable<TrackDto> tracks = await mediator.Send(new GetTracksByPlaylistIdRequest(playlist.Id));
        List<TrackDto> list = tracks.ToList();
        if (list.Count == 0)
            return false;

        playerService.LoadPlaylist(list);
        playerService.Play();
        return true;
    }


    public async Task<bool> ListenAlbumAsync(string albumName)
    {
        IEnumerable<AlbumDto> albums = await mediator.Send(new GetAllAlbumsRequest());
        AlbumDto? album = albums.FirstOrDefault(p => p.Name.Equals(albumName, StringComparison.OrdinalIgnoreCase));

        if (album is null)
            return false;

        Result<IEnumerable<TrackDto>> result = await mediator.Send(new GetTracksByAlbumIdRequest(album.Id));
        return result.IsSuccess && PlayTracks(result.Value);
    }


    public async Task<bool> ListenArtistAsync(string artistName)
    {
        IEnumerable<ArtistDto> artists = await mediator.Send(new GetAllArtistsRequest());
        ArtistDto? artist = artists.FirstOrDefault(p => p.Name.Equals(artistName, StringComparison.OrdinalIgnoreCase));

        if (artist is null)
            return false;

        Result<IEnumerable<TrackDto>> result = await mediator.Send(new GetTracksByArtistIdRequest(artist.Id));
        return result.IsSuccess && PlayTracks(result.Value);
    }

    public async Task<bool> ListenGenreAsync(string genreName)
    {
        IEnumerable<GenreDto> genres = await mediator.Send(new GetAllGenresRequest());
        GenreDto? genre = genres.FirstOrDefault(p => p.Name.Equals(genreName, StringComparison.OrdinalIgnoreCase));

        if (genre is null)
            return false;

        Result<IEnumerable<TrackDto>> result = await mediator.Send(new GetTracksByGenreIdRequest(genre.Id));
        return result.IsSuccess && PlayTracks(result.Value);
    }


    private bool PlayTracks(IEnumerable<TrackDto> tracks)
    {
        List<TrackDto> list = tracks.ToList();
        if (list.Count == 0)
            return false;

        TracksRandomizer.Randomize(list);
        list = list.Take(MaxTracks).ToList();

        playerService.LoadPlaylist(list);
        playerService.Play();

        return true;
    }
}