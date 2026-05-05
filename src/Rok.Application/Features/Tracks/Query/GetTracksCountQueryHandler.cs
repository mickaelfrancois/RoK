using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Query;

public class GetTracksCountQuery : IQuery<int>
{
}

public class GetTracksCountQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetTracksCountQuery, int>
{
    public Task<int> HandleAsync(GetTracksCountQuery request, CancellationToken cancellationToken)
    {
        return _trackRepository.CountAsync();
    }
}
