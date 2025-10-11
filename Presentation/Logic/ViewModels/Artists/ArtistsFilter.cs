namespace Rok.Logic.ViewModels.Artists;

public class ArtistsFilter(ResourceLoader _resourceLoader)
{
    public const string KFilterByFavoriteArtist = "ARTISTFAVORITE";
    public const string KFilterByGenreArtist = "GENREFAVORITE";
    public const string KFilterByNeverListened = "NEVERLISTENED";

    public static IEnumerable<ArtistViewModel> FilterByGenreId(long genreId, IEnumerable<ArtistViewModel> artists)
    {
        if (genreId == 0)
            return artists;

        artists = artists.Where(artist => artist.Artist.GenreId == genreId);

        return artists;
    }

    public static IEnumerable<ArtistViewModel> Filter(string filterBy, IEnumerable<ArtistViewModel> artists)
    {
        switch (filterBy)
        {
            case KFilterByFavoriteArtist:
                artists = artists.Where(artist => artist.Artist.IsFavorite);
                break;

            case KFilterByGenreArtist:
                artists = artists.Where(artist => artist.Artist.IsGenreFavorite);
                break;

            case KFilterByNeverListened:
                artists = artists.Where(artist => artist.Artist.ListenCount == 0);
                break;
        }

        return artists;
    }

    public string GetLabel(string filterBy)
    {
        string label = filterBy switch
        {
            KFilterByFavoriteArtist => _resourceLoader.GetString("artistsViewFilterByFavoriteArtist"),
            KFilterByGenreArtist => _resourceLoader.GetString("artistsViewFilterByFavoriteGenre"),
            KFilterByNeverListened => _resourceLoader.GetString("artistsViewFilterByNeverListened"),
            _ => _resourceLoader.GetString("artistsViewFilterNone"),
        };

        return label;
    }
}
