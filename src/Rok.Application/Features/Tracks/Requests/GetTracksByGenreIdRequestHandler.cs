using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksByGenreIdRequest(long genreId) : IRequest<IEnumerable<TrackDto>>
{
    public long GenreId { get; } = genreId;
}


public sealed class GetTracksByGenreIdRequestValidator : Validator<GetTracksByGenreIdRequest>
{
    public GetTracksByGenreIdRequestValidator()
    {
        Rule(x => x.GenreId).GreaterThan(0L);
    }
}


public class GetTracksByGenreIdRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksByGenreIdRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetTracksByGenreIdRequest query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByGenreIdAsync(query.GenreId);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}
