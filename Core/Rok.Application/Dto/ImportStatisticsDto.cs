namespace Rok.Application.Dto;

public class ImportStatisticsDto
{
    public int FilesRead { get; set; }

    public int TracksImported { get; set; }

    public int TracksUpdated { get; set; }

    public int AlbumImported { get; set; }

    public int ArtistImported { get; set; }

    public int GenreImported { get; set; }

    public bool HasAnyImport
    {
        get
        {
            return TracksImported > 0 || AlbumImported > 0 || ArtistImported > 0 || GenreImported > 0 || TracksUpdated > 0;
        }
    }

    public int TotalCount
    {
        get
        {
            return TracksImported + AlbumImported + ArtistImported + GenreImported;
        }
    }
}
