using Rok.Application.Interfaces;

namespace Rok.Application.Services.Filters;

public class AlbumsFilter(IResourceService resourceLoader) : FilterService<IFilterableAlbum>(resourceLoader)
{
    public const string KFilterByAlbumFavorite = "ALBUMFAVORITE";
    public const string KFilterByArtistFavorite = "ARTISTFAVORITE";
    public const string KFilterByGenreFavorite = "GENREFAVORITE";
    public const string KFilterByNeverListened = "NEVERLISTENED";
    public const string KFilterByLive = "LIVE";
    public const string KFilterByBestOf = "BESTOF";
    public const string KFilterByCompilation = "COMPILATION";
    public const string KFilterByAlbum = "ALBUM";

    protected override void RegisterFilterStrategies()
    {
        RegisterFilter(KFilterByAlbumFavorite, albums => albums.Where(a => a.IsFavorite));
        RegisterFilter(KFilterByArtistFavorite, albums => albums.Where(a => a.IsArtistFavorite));
        RegisterFilter(KFilterByGenreFavorite, albums => albums.Where(a => a.IsGenreFavorite));
        RegisterFilter(KFilterByNeverListened, albums => albums.Where(a => a.ListenCount == 0));
        RegisterFilter(KFilterByLive, albums => albums.Where(a => a.IsLive));
        RegisterFilter(KFilterByBestOf, albums => albums.Where(a => a.IsBestOf));
        RegisterFilter(KFilterByCompilation, albums => albums.Where(a => a.IsCompilation));
        RegisterFilter(KFilterByAlbum, albums => albums.Where(a => !a.IsCompilation && !a.IsLive && !a.IsBestOf));
    }

    public override string GetLabel(string filterBy)
    {
        return filterBy switch
        {
            KFilterByArtistFavorite => ResourceLoader.GetString("albumsViewFilterByFavoriteArtist"),
            KFilterByAlbumFavorite => ResourceLoader.GetString("albumsViewFilterByFavoriteAlbum"),
            KFilterByGenreFavorite => ResourceLoader.GetString("albumsViewFilterByFavoriteGenre"),
            KFilterByNeverListened => ResourceLoader.GetString("albumsViewFilterByNeverListened"),
            KFilterByLive => ResourceLoader.GetString("albumsViewFilterByLive"),
            KFilterByBestOf => ResourceLoader.GetString("albumsViewFilterByBestof"),
            KFilterByCompilation => ResourceLoader.GetString("albumsViewFilterByCompilation"),
            KFilterByAlbum => ResourceLoader.GetString("albumsViewFilterByAlbum"),
            _ => ResourceLoader.GetString("albumsViewFilterNone"),
        };
    }
}