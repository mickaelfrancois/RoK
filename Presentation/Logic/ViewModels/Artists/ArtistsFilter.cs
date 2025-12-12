namespace Rok.Logic.ViewModels.Artists;

public class ArtistsFilter : FilterService<ArtistViewModel>
{
    public const string KFilterByFavoriteArtist = "ARTISTFAVORITE";
    public const string KFilterByGenreFavorite = "GENREFAVORITE";
    public const string KFilterByNeverListened = "NEVERLISTENED";

    public ArtistsFilter(ResourceLoader resourceLoader) : base(resourceLoader)
    {
    }

    public IEnumerable<ArtistViewModel> FilterByGenreId(long genreId, IEnumerable<ArtistViewModel> artists)
    {
        return FilterByGenreId(genreId, artists, a => a.Artist.GenreId);
    }

    protected override void RegisterFilterStrategies()
    {
        RegisterFilter(KFilterByFavoriteArtist,
            artists => FilterByFavorite(artists, a => a.Artist.IsFavorite));

        RegisterFilter(KFilterByGenreFavorite,
            artists => FilterByFavorite(artists, a => a.Artist.IsGenreFavorite));

        RegisterFilter(KFilterByNeverListened,
            artists => FilterByNeverListened(artists, a => a.Artist.ListenCount));
    }

    public override string GetLabel(string filterBy)
    {
        return filterBy switch
        {
            KFilterByFavoriteArtist => ResourceLoader.GetString("artistsViewFilterByFavoriteArtist"),
            KFilterByGenreFavorite => ResourceLoader.GetString("artistsViewFilterByFavoriteGenre"),
            KFilterByNeverListened => ResourceLoader.GetString("artistsViewFilterByNeverListened"),
            _ => ResourceLoader.GetString("artistsViewFilterNone"),
        };
    }
}