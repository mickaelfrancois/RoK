using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetAllTracksRequest : IRequest<IEnumerable<TrackDto>>
{
}

public class GetAllTracksRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetAllTracksRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetAllTracksRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetAllAsync();

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}