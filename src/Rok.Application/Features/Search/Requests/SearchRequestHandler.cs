using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Features.Search.Requests;

public class SearchRequest : IRequest<SearchDto>
{
    public string Name { get; set; } = string.Empty;
}

public sealed class SearchRequestValidator : Validator<SearchRequest>
{
    public SearchRequestValidator() { Rule(x => x.Name).Required(); }
}

public class SearchRequestHandler(IAlbumRepository albumRepository, IArtistRepository artistRepository, ITrackRepository trackRepository) : IRequestHandler<SearchRequest, SearchDto>
{
    public async Task<SearchDto> Handle(SearchRequest request, CancellationToken cancellationToken)
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