using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksByAlbumListRequest : IRequest<IEnumerable<TrackDto>>
{
    public List<long> AlbumsId { get; set; } = [];

    public int Limit { get; set; } = 100;
}

public class GetTracksByAlbumListRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksByAlbumListRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetTracksByAlbumListRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByAlbumIdAsync(request.AlbumsId, request.Limit);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}