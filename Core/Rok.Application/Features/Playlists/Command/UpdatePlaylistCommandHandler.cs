using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists.Command;

public class UpdatePlaylistCommand : ICommand<Result>
{
    [Required]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Picture { get; set; } = string.Empty;

    public int TrackCount { get; set; }

    public long Duration { get; set; }

    public int TrackMaximum { get; set; }

    public long DurationMaximum { get; set; }

    public List<PlaylistGroupDto> Groups { get; set; } = [];

    public SmartPlaylistSelectBy Sorts { get; set; }

    public int Type { get; set; } = 0;
}

public class UpdatePlaylistCommandHandler(IPlaylistHeaderRepository _repository, ILogger<UpdatePlaylistCommandHandler> _logger) : ICommandHandler<UpdatePlaylistCommand, Result>
{
    public async Task<Result> HandleAsync(UpdatePlaylistCommand message, CancellationToken cancellationToken)
    {
        PlaylistHeaderEntity playlistEntity = PlaylistHeadeDtoMapping.Map(message);

        _logger.LogTrace("Playlist json: {Json}", playlistEntity.GroupsJson);

        bool isSuccess = await _repository.UpdateAsync(playlistEntity);

        if (isSuccess)
            return Result.Success();
        else
            return Result.Fail("Failed to update playlist.");
    }
}
