using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksByAlbumIdRequest(long albumId) : IRequest<Result<IEnumerable<TrackDto>>>
{
    public long AlbumId { get; } = albumId;
}


public sealed class GetTracksByAlbumIdRequestValidator : Validator<GetTracksByAlbumIdRequest>
{
    public GetTracksByAlbumIdRequestValidator()
    {
        Rule(x => x.AlbumId).GreaterThan(0L);
    }
}


public class GetTracksByAlbumIdRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>
{
    public async Task<Result<IEnumerable<TrackDto>>> Handle(GetTracksByAlbumIdRequest query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByAlbumIdAsync(query.AlbumId);

        return Result<IEnumerable<TrackDto>>.Ok(tracks.Select(a => TrackDtoMapping.Map(a)));
    }
}