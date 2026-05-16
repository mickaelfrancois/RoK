using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksCountRequest : IRequest<int>
{
}

public class GetTracksCountRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksCountRequest, int>
{
    public Task<int> Handle(GetTracksCountRequest request, CancellationToken cancellationToken)
    {
        return _trackRepository.CountAsync();
    }
}