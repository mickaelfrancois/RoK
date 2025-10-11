using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetTracksByAlbumListQuery : IQuery<IEnumerable<TrackDto>>
{
    public List<long> AlbumsId { get; set; } = [];

    public int Limit { get; set; } = 100;
}

public class GetTracksByAlbumListQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetTracksByAlbumListQuery, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> HandleAsync(GetTracksByAlbumListQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByAlbumIdAsync(request.AlbumsId, request.Limit);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}
