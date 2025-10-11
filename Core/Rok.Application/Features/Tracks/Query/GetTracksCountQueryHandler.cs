using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetTracksCountQuery : IQuery<int>
{
}

public class GetTracksCountQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetTracksCountQuery, int>
{
    public async Task<int> HandleAsync(GetTracksCountQuery request, CancellationToken cancellationToken)
    {
        return await _trackRepository.CountAsync();
    }
}
