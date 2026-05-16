using Rok.Application.Interfaces.Repositories;
using Rok.Application.Randomizer;

namespace Rok.Application.Features.Playlists.Requests;

public class GeneratePlaylistTracksRequest : IRequest<IEnumerable<TrackDto>>
{
    public required int PlaylistTrackCount { get; set; }

    public required PlaylistGroupDto Group { get; set; }
}

public class GeneratePlaylistTracksRequestHandler(IPlaylistTrackGenerateRepository _repository) : IRequestHandler<GeneratePlaylistTracksRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GeneratePlaylistTracksRequest request, CancellationToken cancellationToken)
    {
        List<TrackEntity> tracks = await _repository.GenerateAsync(request);

        var trackDto = tracks.Select(a => TrackDtoMapping.Map(a)).ToList();
        TracksRandomizer.ArtistBalancedTrackRandomize(trackDto, 0);
        return trackDto;
    }
}