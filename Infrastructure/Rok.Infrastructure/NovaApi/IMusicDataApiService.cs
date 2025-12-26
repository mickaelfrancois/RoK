namespace Rok.Infrastructure.NovaApi;

public interface IMusicDataApiService
{
    Task<MusicDataArtistDto?> GetArtistAsync(string artistName, string? musicBrainzId);

    Task<MusicDataAlbumDto?> GetAlbumAsync(string albumName, string artistName, string? musicBrainzId);

    Task<MusicDataLyricsDto?> GetLyricsAsync(string artistName, string albumName, string title, int duration);
}
