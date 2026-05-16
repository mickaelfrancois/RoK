using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTracksByPlaylistIdRequest(long playlistId) : IRequest<IEnumerable<TrackDto>>
{
    public long PlaylistId { get; } = playlistId;
}


public sealed class GetTracksByPlaylistIdRequestValidator : Validator<GetTracksByPlaylistIdRequest>
{
    public GetTracksByPlaylistIdRequestValidator()
    {
        Rule(x => x.PlaylistId).GreaterThan(0L);
    }
}


public class GetTracksByPlaylistIdRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<GetTracksByPlaylistIdRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetTracksByPlaylistIdRequest query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByPlaylistIdAsync(query.PlaylistId);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}
