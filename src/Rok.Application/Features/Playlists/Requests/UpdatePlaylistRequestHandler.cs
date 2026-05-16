using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists.Requests;

public class UpdatePlaylistRequest : IRequest<Result>
{
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

public sealed class UpdatePlaylistRequestValidator : Validator<UpdatePlaylistRequest>
{
    public UpdatePlaylistRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class UpdatePlaylistRequestHandler(IPlaylistHeaderRepository _repository, ILogger<UpdatePlaylistRequestHandler> _logger) : IRequestHandler<UpdatePlaylistRequest, Result>
{
    public async Task<Result> Handle(UpdatePlaylistRequest message, CancellationToken cancellationToken)
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
