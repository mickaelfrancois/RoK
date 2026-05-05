using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Insights.Query;

public class GetInsightsQuery : IQuery<InsightsDto>
{
    public DateTime Month { get; init; }
}


internal class GetInsightsQueryHandler(IListeningEventRepository repository) : IQueryHandler<GetInsightsQuery, InsightsDto>
{
    public Task<InsightsDto> HandleAsync(GetInsightsQuery request, CancellationToken cancellationToken)
    {
        return repository.GetInsightsAsync(request.Month);
    }
}
