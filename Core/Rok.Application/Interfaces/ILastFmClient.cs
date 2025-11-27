namespace Rok.Application.Interfaces;

public interface ILastFmClient
{
    string GetArtistPageUrl(string artistName);

    string GetAlbumPageUrl(string artistName, string albumName);

    Task<bool> IsArtistPageAvailableAsync(string artistName, CancellationToken cancellationToken = default);

    Task<bool> IsAlbumPageAvailableAsync(string artistName, string albumName, CancellationToken cancellationToken = default);
}