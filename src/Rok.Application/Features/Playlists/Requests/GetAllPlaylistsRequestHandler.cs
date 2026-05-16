using Rok.Application.Interfaces.Repositories;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists.Requests;

public class GetAllPlaylistsRequest : IRequest<IEnumerable<PlaylistHeaderDto>>
{
    public PlaylistType? FilterType { get; set; } = null;
}


public class GetAllPlaylistsRequestHandler(IPlaylistHeaderRepository _playlistHeaderRepository) : IRequestHandler<GetAllPlaylistsRequest, IEnumerable<PlaylistHeaderDto>>
{
    public async Task<IEnumerable<PlaylistHeaderDto>> Handle(GetAllPlaylistsRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<PlaylistHeaderEntity> playlists = await _playlistHeaderRepository.GetAllAsync();

        if (request.FilterType.HasValue)
            playlists = playlists.Where(p => p.Type == (int)request.FilterType.Value);

        return playlists.Select(a => PlaylistHeadeDtoMapping.Map(a));
    }
}