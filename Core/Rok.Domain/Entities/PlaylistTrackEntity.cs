namespace Rok.Domain.Entities;

[Table("PlalistTracks")]
public class PlaylistTrackEntity : BaseEntity
{
    public long PlaylistId { get; set; }

    public long TrackId { get; set; }

    public int Position { get; set; }

    public bool Listened { get; set; }
}
