using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class CountryRepositoryTests
{
    private static CountryRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<CountryRepository>.Instance, TimeProvider.System);

    [Fact(DisplayName = "GetById should return seeded country")]
    public async Task GetById_ShouldReturnSeededCountry()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        CountryRepository repo = CreateRepository(fixture);

        // Act
        CountryEntity? result = await repo.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FR", result!.Code);
    }

    [Fact(DisplayName = "GetAll should return every country from the database")]
    public async Task GetAll_ShouldReturnEveryCountry_FromDatabase()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        CountryRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync(
            "INSERT INTO Countries(id, code, creatDate) VALUES (@id, @code, @creatDate)",
            new { id = 2, code = "US", creatDate = DateTime.UtcNow });

        // Act
        List<CountryEntity> result = (await repo.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Code == "US");
    }

    [Fact(DisplayName = "GetByName should filter on the name column")]
    public async Task GetByName_ShouldFilterOnNameColumn()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        CountryRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync(
            "UPDATE Countries SET name = 'France' WHERE id = 1");

        // Act
        CountryEntity? result = await repo.GetByNameAsync("France");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }
}