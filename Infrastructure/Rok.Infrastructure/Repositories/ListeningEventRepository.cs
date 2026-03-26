using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

public class ListeningEventRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<ListeningEventRepository> logger) : GenericRepository<ListeningEventEntity>(connection, backgroundConnection, null, logger), IListeningEventRepository
{
    public override string GetTableName()
    {
        return "listeningevents";
    }
}
