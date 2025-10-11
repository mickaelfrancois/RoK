using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Query;

public class GetTracksByPlaylistIdQuery(long playlistId) : IQuery<IEnumerable<TrackDto>>
{
    [RequiredGreaterThanZero]
    public long PlaylistId { get; } = playlistId;
}


public class GetTracksByPlaylistIdQueryHandler(ITrackRepository _trackRepository) : IQueryHandler<GetTracksByPlaylistIdQuery, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> HandleAsync(GetTracksByPlaylistIdQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks = await _trackRepository.GetByPlaylistIdAsync(query.PlaylistId);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}