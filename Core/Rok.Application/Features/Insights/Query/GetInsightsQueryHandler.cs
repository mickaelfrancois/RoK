using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Insights.Query;

public class GetInsightsQuery : IQuery<InsightsDto>
{
    public DateTime Month { get; init; }
}


internal class GetInsightsQueryHandler(IListeningEventRepository repository) : IQueryHandler<GetInsightsQuery, InsightsDto>
{
    public async Task<InsightsDto> HandleAsync(GetInsightsQuery request, CancellationToken cancellationToken)
    {
        return await repository.GetInsightsAsync(request.Month);
    }
}
