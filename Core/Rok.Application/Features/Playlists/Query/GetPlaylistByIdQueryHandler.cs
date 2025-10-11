using Rok.Application.Interfaces;

namespace Rok.Application.Features.Playlists.Query;

public class GetPlaylistByIdQuery(long id) : IQuery<Result<PlaylistHeaderDto>>
{
    [RequiredGreaterThanZero]
    public long Id { get; } = id;
}


public class GetPlaylistByIdQueryHandler(IPlaylistHeaderRepository _playlistHeaderRepository) : IQueryHandler<GetPlaylistByIdQuery, Result<PlaylistHeaderDto>>
{
    public async Task<Result<PlaylistHeaderDto>> HandleAsync(GetPlaylistByIdQuery query, CancellationToken cancellationToken)
    {
        PlaylistHeaderEntity? playlist = await _playlistHeaderRepository.GetByIdAsync(query.Id);

        if (playlist == null)
            return Result<PlaylistHeaderDto>.Fail("NotFound", "Playlist not found");
        else
            return Result<PlaylistHeaderDto>.Success(PlaylistHeadeDtoMapping.Map(playlist));
    }
}