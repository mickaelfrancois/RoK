using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Playlists.Requests;

public class GetPlaylistByIdRequest(long id) : IRequest<Result<PlaylistHeaderDto>>
{
    public long Id { get; } = id;
}

public sealed class GetPlaylistByIdRequestValidator : Validator<GetPlaylistByIdRequest>
{
    public GetPlaylistByIdRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class GetPlaylistByIdRequestHandler(IPlaylistHeaderRepository _playlistHeaderRepository) : IRequestHandler<GetPlaylistByIdRequest, Result<PlaylistHeaderDto>>
{
    public async Task<Result<PlaylistHeaderDto>> Handle(GetPlaylistByIdRequest query, CancellationToken cancellationToken)
    {
        PlaylistHeaderEntity? playlist = await _playlistHeaderRepository.GetByIdAsync(query.Id);

        if (playlist == null)
            return Result<PlaylistHeaderDto>.Fail("NotFound", "Playlist not found");
        else
            return Result<PlaylistHeaderDto>.Success(PlaylistHeadeDtoMapping.Map(playlist));
    }
}