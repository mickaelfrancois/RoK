using Rok.Application.Dto.MusicDataApi;

namespace Rok.Application.Interfaces;

public interface IMusicDataApiService
{
    Task<MusicDataArtistDto?> GetArtistAsync(string artistName, string? musicBrainzId);

    Task<MusicDataAlbumDto?> GetAlbumAsync(string albumName, string artistName, string? musicBrainzId, string? artistMusicBrainzId);

    Task<MusicDataLyricsDto?> GetLyricsAsync(string artistName, string albumName, string title, long duration);

    Task DownloadArtistPictureAsync(MusicDataArtistDto artist, string artistFile, CancellationToken cancellationToken);

    Task DownloadArtistBackdropsAsync(MusicDataArtistDto artist, string artistFolder, CancellationToken cancellationToken);

    Task DownloadCoverAsync(MusicDataAlbumDto album, string coverFile, CancellationToken cancellationToken);

    bool IsApiRetryAllowed(DateTime? lastAttempt);
}
