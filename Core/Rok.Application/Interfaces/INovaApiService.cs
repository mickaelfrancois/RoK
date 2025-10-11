using Rok.Application.Dto.NovaApi;

namespace Rok.Application.Interfaces;

public interface INovaApiService
{
    bool IsEnable { get; set; }

    Task<ApiArtistModel?> GetArtistAsync(string artistName);

    Task GetArtistPictureAsync(string musicBrainzID, string category, string artistFile);

    Task GetArtistBackdropsAsync(string musicBrainzId, int fanartsCount, string artistFolder);

    Task<bool> GetArtistBackdropAsync(string url, string artistFolder);

    Task<ApiAlbumModel?> GetAlbumAsync(string albumName, string artistName);

    Task GetAlbumPicturesAsync(string musicBrainzID, string albumFile);

    Task<ApiLyricsModel?> GetLyricsAsync(string artistName, string title);
}
