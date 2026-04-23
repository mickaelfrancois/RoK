using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Genres.Query;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;
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


    public void Toggle()
    {
        if (playerService.PlaybackState == EPlaybackState.Playing)
            playerService.Pause();
        else
            playerService.Play();
    }


    public async Task<bool> ListenPlaylistAsync(string playlistName)
    {
        IEnumerable<PlaylistHeaderDto> playlists = await mediator.SendMessageAsync(new GetAllPlaylistsQuery());
        PlaylistHeaderDto? playlist = playlists.FirstOrDefault(p => p.Name.Equals(playlistName, StringComparison.OrdinalIgnoreCase));

        if (playlist is null)
            return false;

        IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetTracksByPlaylistIdQuery(playlist.Id));
        List<TrackDto> list = tracks.ToList();
        if (list.Count == 0)
            return false;

        playerService.LoadPlaylist(list);
        playerService.Play();
        return true;
    }


    public async Task<bool> ListenAlbumAsync(string albumName)
    {
        IEnumerable<AlbumDto> albums = await mediator.SendMessageAsync(new GetAllAlbumsQuery());
        AlbumDto? album = albums.FirstOrDefault(p => p.Name.Equals(albumName, StringComparison.OrdinalIgnoreCase));

        if (album is null)
            return false;

        IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetTracksByAlbumIdQuery(album.Id));
        return PlayTracks(tracks);
    }


    public async Task<bool> ListenArtistAsync(string artistName)
    {
        IEnumerable<ArtistDto> artists = await mediator.SendMessageAsync(new GetAllArtistsQuery());
        ArtistDto? artist = artists.FirstOrDefault(p => p.Name.Equals(artistName, StringComparison.OrdinalIgnoreCase));

        if (artist is null)
            return false;

        IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetTracksByArtistIdQuery(artist.Id));
        return PlayTracks(tracks);
    }

    public async Task<bool> ListenGenreAsync(string genreName)
    {
        IEnumerable<GenreDto> genres = await mediator.SendMessageAsync(new GetAllGenresQuery());
        GenreDto? genre = genres.FirstOrDefault(p => p.Name.Equals(genreName, StringComparison.OrdinalIgnoreCase));

        if (genre is null)
            return false;

        IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetTracksByGenreIdQuery(genre.Id));
        return PlayTracks(tracks);
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