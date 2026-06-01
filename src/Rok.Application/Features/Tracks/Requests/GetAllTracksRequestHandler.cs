using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;
using Rok.Shared;

namespace Rok.Application.Features.Tracks.Requests;

public class GetAllTracksRequest : IRequest<IEnumerable<TrackDto>>
{
}

public class GetAllTracksRequestHandler(ITrackRepository _trackRepository, ILogger<GetAllTracksRequestHandler> _logger) : IRequestHandler<GetAllTracksRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetAllTracksRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks;

        using (new PerfLogger(_logger).Parameters("Tracks: DB fetch"))
        {
            tracks = await _trackRepository.GetAllAsync();
        }

        using (new PerfLogger(_logger).Parameters("Tracks: DTO map"))
        {
            return tracks.Select(a => TrackDtoMapping.Map(a)).ToList();
        }
    }
}
