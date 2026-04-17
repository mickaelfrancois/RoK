using Rok.Application.Interfaces.Repositories;
using Rok.Application.Randomizer;

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

        var trackDto = tracks.Select(a => TrackDtoMapping.Map(a)).ToList();
        TracksRandomizer.ArtistBalancedTrackRandomize(trackDto, 0);
        return trackDto;
    }
}