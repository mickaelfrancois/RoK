using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksByArtistIdRequest(long artistId) : IRequest<IEnumerable<TrackDto>>
{
    public long ArtistId { get; } = artistId;
}


public sealed class GetTracksByArtistIdRequestValidator : Validator<GetTracksByArtistIdRequest>
{
    public GetTracksByArtistIdRequestValidator()
    {
        Rule(x => x.ArtistId).GreaterThan(0L);
    }
}


public class GetTracksByArtistIdRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksByArtistIdRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetTracksByArtistIdRequest query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByArtistIdAsync(query.ArtistId);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}
