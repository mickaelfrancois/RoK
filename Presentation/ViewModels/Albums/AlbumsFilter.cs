using Rok.Logic.ViewModels.Albums;
using Rok.Services;

namespace Rok.ViewModels.Albums;

public class AlbumsFilter(ResourceLoader resourceLoader) : FilterService<AlbumViewModel>(resourceLoader)
{
    public const string KFilterByAlbumFavorite = "ALBUMFAVORITE";
    public const string KFilterByArtistFavorite = "ARTISTFAVORITE";
    public const string KFilterByGenreFavorite = "GENREFAVORITE";
    public const string KFilterByNeverListened = "NEVERLISTENED";
    public const string KFilterByLive = "LIVE";
    public const string KFilterByBestOf = "BESTOF";
    public const string KFilterByCompilation = "COMPILATION";
    public const string KFilterByAlbum = "ALBUM";

    public IEnumerable<AlbumViewModel> FilterByGenreId(long genreId, IEnumerable<AlbumViewModel> albums)
    {
        return FilterByGenreId(genreId, albums, a => a.Album.GenreId);
    }

    public IEnumerable<AlbumViewModel> FilterByTags(List<string> tags, IEnumerable<AlbumViewModel> albums)
    {
        return FilterByTags(tags, albums, a => a.Album.GetTags());
    }

    protected override void RegisterFilterStrategies()
    {
        RegisterFilter(KFilterByAlbumFavorite,
            albums => FilterByFavorite(albums, a => a.Album.IsFavorite));

        RegisterFilter(KFilterByArtistFavorite,
            albums => FilterByFavorite(albums, a => a.Album.IsArtistFavorite));

        RegisterFilter(KFilterByGenreFavorite,
            albums => FilterByFavorite(albums, a => a.Album.IsGenreFavorite));

        RegisterFilter(KFilterByNeverListened,
            albums => FilterByNeverListened(albums, a => a.Album.ListenCount));

        RegisterFilter(KFilterByLive,
            albums => FilterByCondition(albums, a => a.Album.IsLive));

        RegisterFilter(KFilterByBestOf,
            albums => FilterByCondition(albums, a => a.Album.IsBestOf));

        RegisterFilter(KFilterByCompilation,
            albums => FilterByCondition(albums, a => a.Album.IsCompilation));

        RegisterFilter(KFilterByAlbum,
            albums => FilterByCondition(albums, a => !a.Album.IsCompilation && !a.Album.IsLive && !a.Album.IsBestOf));
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