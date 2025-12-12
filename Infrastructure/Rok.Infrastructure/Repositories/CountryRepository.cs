using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Repositories;

public class CountryRepository(IDbConnection db, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, ILogger<CountryRepository> logger) : GenericRepository<CountryEntity>(db, backgroundDb, null, logger), ICountryRepository
{
    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT countries.*                     
                     FROM countries                     
                """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE countries.{whereParam} = @{whereParam}";

        return query;
    }
}
