using Rok.Application.Interfaces;
using Rok.Domain.Entities;

namespace Rok.Import;

/// <summary>
/// Provides a cache for country data, allowing efficient retrieval of country information by code.
/// </summary>
/// <remarks>The <see cref="CountryCache"/> class is designed to cache country data retrieved from an <see
/// cref="ICountryRepository"/>. It supports asynchronous loading of the cache and provides a method to retrieve a
/// country's ID by its code.</remarks>
/// <param name="_countryRepository"></param>
public class CountryCache(ICountryRepository _countryRepository)
{
    private readonly Dictionary<string, CountryEntity> _countriesCache = new(StringComparer.InvariantCultureIgnoreCase);


    /// <summary>
    /// Asynchronously loads country data into the cache.
    /// </summary>
    /// <remarks>This method clears the existing cache and repopulates it with the latest country data
    /// retrieved from the repository. The cache is keyed by the lowercase country code.</remarks>
    /// <returns>A task that represents the asynchronous load operation.</returns>
    public async Task LoadCacheAsync()
    {
        _countriesCache.Clear();

        IEnumerable<CountryEntity> countries = await _countryRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (CountryEntity country in countries)
        {
            _countriesCache.TryAdd(country.Code.ToLower(), country);
        }
    }


    /// <summary>
    /// Retrieves the unique identifier for a country based on its code.
    /// </summary>
    /// <remarks>This method uses a cached collection to perform the lookup, ensuring efficient retrieval of
    /// country identifiers.</remarks>
    /// <param name="countryCode">The ISO country code used to look up the country identifier. Cannot be null or empty.</param>
    /// <returns>The unique identifier of the country if the code is found; otherwise, <see langword="null"/>.</returns>
    public long? GetCountryIdFromCode(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;

        if (_countriesCache.TryGetValue(countryCode, out CountryEntity? country))
            return country.Id;
        else
            return null;
    }
}
