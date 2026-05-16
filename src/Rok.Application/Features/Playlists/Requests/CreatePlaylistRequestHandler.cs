using Rok.Application.Interfaces.Repositories;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists.Requests;

public class CreatePlaylistRequest : IRequest<Result<long>>
{
    public string Name { get; set; } = string.Empty;

    public string Picture { get; set; } = string.Empty;

    public int TrackMaximum { get; set; } = 100;

    public long DurationMaximum { get; set; } = 120;

    public int Type { get; set; }

    public List<PlaylistGroupDto> Groups { get; set; } = [];

    public SmartPlaylistSelectBy Sorts { get; set; }
}

public class CreatePlaylistRequestHandler(IPlaylistHeaderRepository _repository) : IRequestHandler<CreatePlaylistRequest, Result<long>>
{
    public async Task<Result<long>> Handle(CreatePlaylistRequest message, CancellationToken cancellationToken)
    {
        PlaylistHeaderEntity playlistEntity = PlaylistHeadeDtoMapping.Map(message);

        long id = await _repository.AddAsync(playlistEntity);

        if (id > 0)
            return Result<long>.Ok(id);
        else
            return Result<long>.Fail(new OperationError("playlist.create_failed", "Failed to create playlist."));
    }
}
