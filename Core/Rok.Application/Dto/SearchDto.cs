namespace Rok.Application.Dto;

public class SearchDto
{
    public int ResultCount
    {
        get
        {
            return Artists.Count + Albums.Count + Tracks.Count;
        }
    }

    public List<ArtistDto> Artists { get; set; } = [];

    public List<AlbumDto> Albums { get; set; } = [];

    public List<TrackDto> Tracks { get; set; } = [];
}
