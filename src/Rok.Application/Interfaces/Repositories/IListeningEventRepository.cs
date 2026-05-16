using Rok.Application.Features.Insights;

namespace Rok.Application.Interfaces.Repositories;

public interface IListeningEventRepository : IRepository<ListeningEventEntity>
{
    Task<InsightsDto> GetInsightsAsync(DateTime month);
}