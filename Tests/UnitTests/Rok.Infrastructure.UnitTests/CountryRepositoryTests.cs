using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests;

public class CountryRepositoryTests(SqliteDatabaseFixture fixture) : IClassFixture<SqliteDatabaseFixture>
{
    private CountryRepository CreateRepository()
    {
        return new CountryRepository(fixture.Connection, fixture.Connection, NullLogger<CountryRepository>.Instance);
    }

    [Fact]
    public async Task GetAll_ReturnsSeededCountries()
    {
        // Arrange
        CountryRepository repo = CreateRepository();

        // Act
        var all = (await repo.GetAllAsync()).ToList();

        // Assert
        Assert.NotEmpty(all);
        Assert.Contains(all, c => c.Code == "FR" || c.English == "France");
    }

    [Fact]
    public async Task GetById_ReturnsCountry()
    {
        // Arrange
        CountryRepository repo = CreateRepository();

        // Act
        CountryEntity? country = await repo.GetByIdAsync(1);

        // Assert
        Assert.NotNull(country);
        Assert.Equal(1, country!.Id);
        Assert.Equal("FR", country.Code);
    }

    [Fact]
    public async Task AddAsync_AddsCountry()
    {
        // Arrange
        CountryRepository repo = CreateRepository();
        var newCountry = new CountryEntity
        {
            Code = "US",
            English = "United States",
            French = "États-Unis",
            CreatDate = System.DateTime.UtcNow
        };

        // Act
        long id = await repo.AddAsync(newCountry);

        // Assert
        Assert.True(id > 0);
        CountryEntity? fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("US", fetched!.Code);
        Assert.Equal("United States", fetched.English);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCountry()
    {
        // Arrange
        CountryRepository repo = CreateRepository();
        CountryEntity? country = await repo.GetByIdAsync(1);
        Assert.NotNull(country);

        country!.English = "République Française";
        country.EditDate = System.DateTime.UtcNow;

        // Act
        bool ok = await repo.UpdateAsync(country);

        // Assert
        Assert.True(ok);
        CountryEntity? updated = await repo.GetByIdAsync(1);
        Assert.Equal("République Française", updated!.English);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCountry()
    {
        // Arrange
        CountryRepository repo = CreateRepository();
        var toDelete = new CountryEntity
        {
            Code = "XX",
            English = "ToDelete",
            French = "ÀSupprimer",
            CreatDate = System.DateTime.UtcNow
        };

        long id = await repo.AddAsync(toDelete);
        CountryEntity? fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);

        // Act
        bool ok = await repo.DeleteAsync(fetched!);

        // Assert
        Assert.True(ok);
        CountryEntity? after = await repo.GetByIdAsync(id);
        Assert.Null(after);
    }
}