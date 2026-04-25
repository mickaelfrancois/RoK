using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;

namespace Rok.ImportTests;

public class CountryCacheTests
{
    private static CountryCache BuildCache(params CountryEntity[] countries)
    {
        Mock<ICountryRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(countries);
        return new CountryCache(repository.Object);
    }

    [Fact(DisplayName = "GetCountryIdFromCode should return id for loaded country code")]
    public async Task GetCountryIdFromCode_ShouldReturnId_ForLoadedCountryCode()
    {
        // Arrange
        CountryCache cache = BuildCache(
            new CountryEntity { Id = 1, Code = "fr" },
            new CountryEntity { Id = 2, Code = "us" });
        await cache.LoadCacheAsync();

        // Act
        long? result = cache.GetCountryIdFromCode("fr");

        // Assert
        Assert.Equal(1, result);
    }

    [Fact(DisplayName = "GetCountryIdFromCode should lookup in a case-insensitive way")]
    public async Task GetCountryIdFromCode_ShouldLookup_InCaseInsensitiveWay()
    {
        // Arrange
        CountryCache cache = BuildCache(new CountryEntity { Id = 5, Code = "FR" });
        await cache.LoadCacheAsync();

        // Act
        long? result = cache.GetCountryIdFromCode("fr");

        // Assert
        Assert.Equal(5, result);
    }

    [Fact(DisplayName = "GetCountryIdFromCode should return null when code is unknown")]
    public async Task GetCountryIdFromCode_ShouldReturnNull_WhenCodeIsUnknown()
    {
        // Arrange
        CountryCache cache = BuildCache(new CountryEntity { Id = 1, Code = "fr" });
        await cache.LoadCacheAsync();

        // Act
        long? result = cache.GetCountryIdFromCode("zz");

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "GetCountryIdFromCode should return null when code is blank or empty")]
    public async Task GetCountryIdFromCode_ShouldReturnNull_WhenCodeIsBlankOrEmpty()
    {
        // Arrange
        CountryCache cache = BuildCache(new CountryEntity { Id = 1, Code = "fr" });
        await cache.LoadCacheAsync();

        // Act & Assert
        Assert.Null(cache.GetCountryIdFromCode(""));
        Assert.Null(cache.GetCountryIdFromCode("   "));
    }

    [Fact(DisplayName = "LoadCache should replace previous entries on reload")]
    public async Task LoadCache_ShouldReplacePreviousEntries_OnReload()
    {
        // Arrange
        Mock<ICountryRepository> repository = new();
        repository.SetupSequence(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new[] { new CountryEntity { Id = 1, Code = "fr" } })
            .ReturnsAsync(new[] { new CountryEntity { Id = 2, Code = "us" } });
        CountryCache cache = new(repository.Object);

        // Act
        await cache.LoadCacheAsync();
        await cache.LoadCacheAsync();

        // Assert
        Assert.Null(cache.GetCountryIdFromCode("fr"));
        Assert.Equal(2, cache.GetCountryIdFromCode("us"));
    }
}
