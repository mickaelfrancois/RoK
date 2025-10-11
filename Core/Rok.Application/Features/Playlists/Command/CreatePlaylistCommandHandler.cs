using Rok.Application.Interfaces;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists.Command;

public class CreatePlaylistCommand : ICommand<Result<long>>
{
    public string Name { get; set; } = string.Empty;

    public string Picture { get; set; } = string.Empty;

    public int TrackMaximum { get; set; } = 100;

    public long DurationMaximum { get; set; } = 120;

    public int Type { get; set; }

    public List<PlaylistGroupDto> Groups { get; set; } = [];

    public SmartPlaylistSelectBy Sorts { get; set; }
}

public class CreatePlaylistCommandHandler(IPlaylistHeaderRepository _repository) : ICommandHandler<CreatePlaylistCommand, Result<long>>
{
    public async Task<Result<long>> HandleAsync(CreatePlaylistCommand message, CancellationToken cancellationToken)
    {
        PlaylistHeaderEntity playlistEntity = PlaylistHeadeDtoMapping.Map(message);

        long id = await _repository.AddAsync(playlistEntity);

        if (id > 0)
            return Result<long>.Success(id);
        else
            return Result<long>.Fail("Failed to create playlist.");
    }
}
