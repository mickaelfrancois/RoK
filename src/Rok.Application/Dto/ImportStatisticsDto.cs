namespace Rok.Application.Dto;

public class ImportStatisticsDto
{
    public int FilesRead { get; set; }

    public int TracksImported { get; set; }

    public int TracksUpdated { get; set; }

    public int AlbumsImported { get; set; }

    public int ArtistsImported { get; set; }

    public int GenresImported { get; set; }

    public int TracksDeleted { get; set; }

    public int AlbumsDeleted { get; set; }

    public int ArtistsDeleted { get; set; }

    public int GenresDeleted { get; set; }


    public bool HasAnyImport
    {
        get
        {
            return GenresImported > 0 || TracksDeleted > 0 || AlbumsDeleted > 0 || ArtistsDeleted > 0 || TracksImported > 0 || AlbumsImported > 0 || ArtistsImported > 0 || GenresImported > 0 || TracksUpdated > 0;
        }
    }

    public int TotalCount
    {
        get
        {
            return TracksImported + AlbumsImported + ArtistsImported + GenresImported;
        }
    }
}
