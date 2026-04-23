namespace Rok.Application.Dto;

public class CreatePlaylistTracksDto
{
    public long TrackId { get; set; }

    public int Position { get; set; }

    public bool Listened { get; set; }
}
