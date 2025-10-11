using Rok.Shared.Enums;

namespace Rok.Application.Dto;

public class PlaylistGroupDto
{
    public string Name { get; set; } = string.Empty;

    public int Position { get; set; }

    public int TrackCount { get; set; }

    public List<PlaylistFilterDto> Filters { get; set; } = [];

    public SmartPlaylistSelectBy SortBy { get; set; } = SmartPlaylistSelectBy.Random;
}
