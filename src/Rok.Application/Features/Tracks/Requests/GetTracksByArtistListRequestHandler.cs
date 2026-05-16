using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksByArtistListRequest : IRequest<IEnumerable<TrackDto>>
{
    public List<long> ArtistIds { get; set; } = [];

    public int Limit { get; set; } = 100;
}

public class GetTracksByArtistListRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksByArtistListRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetTracksByArtistListRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByArtistIdAsync(request.ArtistIds, request.Limit);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}