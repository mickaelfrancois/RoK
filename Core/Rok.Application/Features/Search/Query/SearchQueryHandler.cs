using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Features.Search.Query;

public class SearchQuery : IQuery<SearchDto>
{
    [Required]
    public string Name { get; set; } = string.Empty;
}

public class SearchQueryHandler(IAlbumRepository albumRepository, IArtistRepository artistRepository, ITrackRepository trackRepository) : IQueryHandler<SearchQuery, SearchDto>
{
    public async Task<SearchDto> HandleAsync(SearchQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<IAlbumEntity> albums = await albumRepository.SearchAsync(request.Name);
        IEnumerable<IArtistEntity> artists = await artistRepository.SearchAsync(request.Name);
        IEnumerable<TrackEntity> tracks = await trackRepository.SearchAsync(request.Name);

        return new SearchDto
        {
            Albums = albums.Select(a => AlbumMapping.ToDto(a)).ToList(),
            Artists = artists.Select(a => ArtistMapping.ToDto(a)).ToList(),
            Tracks = tracks.Select(t => TrackDtoMapping.Map(t)).ToList()
        };
    }
}
