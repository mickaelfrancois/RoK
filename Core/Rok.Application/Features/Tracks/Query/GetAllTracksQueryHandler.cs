using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetAllTracksQuery : IQuery<IEnumerable<TrackDto>>
{
}

public class GetAllTracksQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetAllTracksQuery, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> HandleAsync(GetAllTracksQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetAllAsync();

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}
