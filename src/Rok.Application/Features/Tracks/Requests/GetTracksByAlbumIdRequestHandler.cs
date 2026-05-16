using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksByAlbumIdRequest(long genreId) : IRequest<IEnumerable<TrackDto>>
{
    public long GenreId { get; } = genreId;
}


public sealed class GetTracksByAlbumIdRequestValidator : Validator<GetTracksByAlbumIdRequest>
{
    public GetTracksByAlbumIdRequestValidator()
    {
        Rule(x => x.GenreId).GreaterThan(0L);
    }
}


public class GetTracksByAlbumIdRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksByAlbumIdRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetTracksByAlbumIdRequest query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByAlbumIdAsync(query.GenreId);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}