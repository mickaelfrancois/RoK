namespace Rok.Logic.ViewModels.Albums;

public class AlbumsFilter(ResourceLoader _resourceLoader)
{
    public static IEnumerable<AlbumViewModel> FilterByGenreId(long genreId, IEnumerable<AlbumViewModel> albums)
    {
        if (genreId == 0)
            return albums;

        albums = albums.Where(album => album.Album.GenreId == genreId);

        return albums;
    }


    public static IEnumerable<AlbumViewModel> Filter(string filterBy, IEnumerable<AlbumViewModel> albums)
    {
        switch (filterBy)
        {
            case "ALBUMFAVORITE":
                albums = albums.Where(album => album.Album.IsFavorite);
                break;

            case "ARTISTFAVORITE":
                albums = albums.Where(album => album.Album.IsArtistFavorite);
                break;

            case "GENREFAVORITE":
                albums = albums.Where(album => album.Album.IsGenreFavorite);
                break;

            case "NEVERLISTENED":
                albums = albums.Where(album => album.Album.ListenCount == 0);
                break;

            case "LIVE":
                albums = albums.Where(album => album.Album.IsLive);
                break;

            case "BESTOF":
                albums = albums.Where(album => album.Album.IsBestOf);
                break;

            case "COMPILATION":
                albums = albums.Where(album => album.Album.IsCompilation);
                break;

            case "ALBUM":
                albums = albums.Where(album => !album.Album.IsCompilation && !album.Album.IsLive && !album.Album.IsCompilation);
                break;
        }

        return albums;
    }

    public string GetLabel(string filterBy)
    {
        string label = filterBy switch
        {
            "ARTISTFAVORITE" => _resourceLoader.GetString("albumsViewFilterByFavoriteArtist"),
            "ALBUMFAVORITE" => _resourceLoader.GetString("albumsViewFilterByFavoriteAlbum"),
            "GENREFAVORITE" => _resourceLoader.GetString("albumsViewFilterByFavoriteGenre"),
            "NEVERLISTENED" => _resourceLoader.GetString("albumsViewFilterByNeverListened"),
            "LIVE" => _resourceLoader.GetString("albumsViewFilterByLive"),
            "BESTOF" => _resourceLoader.GetString("albumsViewFilterByBestof"),
            "COMPILATION" => _resourceLoader.GetString("albumsViewFilterByCompilation"),
            "ALBUM" => _resourceLoader.GetString("albumsViewFilterByAlbum"),
            _ => _resourceLoader.GetString("albumsViewFilterNone"),
        };

        return label;
    }
}
