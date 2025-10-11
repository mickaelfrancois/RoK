using Rok.Application.Interfaces;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists.Query;

public class GetAllPlaylistsQuery : IQuery<IEnumerable<PlaylistHeaderDto>>
{
    public PlaylistType? FilterType { get; set; } = null;
}


public class GetAllPlaylistsQueryHandler(IPlaylistHeaderRepository _playlistHeaderRepository) : IQueryHandler<GetAllPlaylistsQuery, IEnumerable<PlaylistHeaderDto>>
{
    public async Task<IEnumerable<PlaylistHeaderDto>> HandleAsync(GetAllPlaylistsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<PlaylistHeaderEntity> playlists = await _playlistHeaderRepository.GetAllAsync();

        if (request.FilterType.HasValue)
            playlists = playlists.Where(p => p.Type == (int)request.FilterType.Value);

        return playlists.Select(a => PlaylistHeadeDtoMapping.Map(a));
    }
}