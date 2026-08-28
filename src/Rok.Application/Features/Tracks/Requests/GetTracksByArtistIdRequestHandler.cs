using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksByArtistIdRequest(long artistId) : IRequest<Result<IEnumerable<TrackDto>>>
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


public class GetTracksByArtistIdRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksByArtistIdRequest, Result<IEnumerable<TrackDto>>>
{
    public async Task<Result<IEnumerable<TrackDto>>> Handle(GetTracksByArtistIdRequest query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByArtistIdAsync(query.ArtistId);

        return Result<IEnumerable<TrackDto>>.Ok(tracks.Select(a => TrackDtoMapping.Map(a)));
    }
}