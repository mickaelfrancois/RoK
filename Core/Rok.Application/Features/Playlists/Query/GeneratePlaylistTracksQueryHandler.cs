using Rok.Application.Interfaces;

namespace Rok.Application.Features.Playlists.Query;

public class GeneratePlaylistTracksQuery : IQuery<IEnumerable<TrackDto>>
{
    public required int PlaylistTrackCount { get; set; }

    public required PlaylistGroupDto Group { get; set; }
}

public class GeneratePlaylistTracksQueryHandler(IPlaylistTrackGenerateRepository _repository) : IQueryHandler<GeneratePlaylistTracksQuery, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> HandleAsync(GeneratePlaylistTracksQuery request, CancellationToken cancellationToken)
    {
        List<TrackEntity> tracks = await _repository.GenerateAsync(request);

        return tracks.Select(a => TrackDtoMapping.Map(a));
    }
}