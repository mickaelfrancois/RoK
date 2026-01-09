using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetTracksByGenreIdQuery(long genreId) : IQuery<IEnumerable<TrackDto>>
{
    [RequiredGreaterThanZero]
    public long GenreId { get; } = genreId;
}


public class GetTracksByGenreIdQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetTracksByGenreIdQuery, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> HandleAsync(GetTracksByGenreIdQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByGenreIdAsync(query.GenreId);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}