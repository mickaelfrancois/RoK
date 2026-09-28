using Rok.Application.Dto;

namespace Rok.Application.Features.Radios.Services;

public interface IRadioBrowserClient
{
    Task<IReadOnlyList<RadioSearchResultDto>> SearchByNameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the most voted working stations of a country.
    /// </summary>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code.</param>
    /// <param name="limit">Maximum number of stations to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<RadioSearchResultDto>> GetTopByCountryAsync(
        string countryCode,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the most voted working stations worldwide.
    /// </summary>
    /// <param name="limit">Maximum number of stations to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<RadioSearchResultDto>> GetTopWorldwideAsync(
        int limit,
        CancellationToken cancellationToken);
}