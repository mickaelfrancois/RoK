using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetTracksByArtistIdQuery(long artistId) : IQuery<IEnumerable<TrackDto>>
{
    [RequiredGreaterThanZero]
    public long ArtistId { get; } = artistId;
}


public class GetTracksByArtistIdQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetTracksByArtistIdQuery, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> HandleAsync(GetTracksByArtistIdQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByArtistIdAsync(query.ArtistId);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}