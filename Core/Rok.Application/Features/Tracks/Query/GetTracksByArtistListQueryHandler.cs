using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetTracksByArtistListQuery : IQuery<IEnumerable<TrackDto>>
{
    public List<long> ArtistIds { get; set; } = [];

    public int Limit { get; set; } = 100;
}

public class GetTracksByArtistListQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetTracksByArtistListQuery, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> HandleAsync(GetTracksByArtistListQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByArtistIdAsync(request.ArtistIds, request.Limit);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}
