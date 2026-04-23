namespace Rok.Application.Dto;

public class PlaylistHeaderDto
{
    public long Id { get; set; }

    public DateTime CreatDate { get; set; }

    public DateTime? EditDate { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Picture { get; set; } = string.Empty;

    public int TrackCount { get; set; }

    public long Duration { get; set; }

    public int TrackMaximum { get; set; }

    public long DurationMaximum { get; set; }

    public string GroupsJson { get; set; } = string.Empty;

    public int Type { get; set; }

    public bool IsSmart => Type == 0;

    public List<PlaylistGroupDto> Groups { get; set; } = [];
}
