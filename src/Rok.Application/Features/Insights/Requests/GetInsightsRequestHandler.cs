using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Insights.Requests;

public class GetInsightsRequest : IRequest<InsightsDto>
{
    public DateTime Month { get; init; }
}


internal class GetInsightsRequestHandler(IListeningEventRepository repository) : IRequestHandler<GetInsightsRequest, InsightsDto>
{
    public Task<InsightsDto> Handle(GetInsightsRequest request, CancellationToken cancellationToken)
    {
        return repository.GetInsightsAsync(request.Month);
    }
}