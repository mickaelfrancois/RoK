using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetTracksByAlbumIdQuery(long genreId) : IQuery<IEnumerable<TrackDto>>
{
    [RequiredGreaterThanZero]
    public long GenreId { get; } = genreId;
}


public class GetTracksByAlbumIdQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetTracksByAlbumIdQuery, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> HandleAsync(GetTracksByAlbumIdQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByAlbumIdAsync(query.GenreId);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}