using System.Text.Json.Serialization;

namespace Rok.Domain.Entities;

[Table("Playlists")]
public class PlaylistHeaderEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Picture { get; set; } = string.Empty;

    public int TrackCount { get; set; }

    public long Duration { get; set; }

    public int TrackMaximum { get; set; }

    public long DurationMaximum { get; set; }

    public string GroupsJson { get; set; } = string.Empty;

    public int Type { get; set; }


    [JsonIgnore]
    [Write(false)]
    public bool IsSmart => Type == 0;
}
