using Rok.Application.Interfaces;

namespace Rok.Application.Services.Filters;

public class AlbumsFilter(IResourceService resourceLoader, TimeProvider timeProvider) : FilterService<IFilterableAlbum>(resourceLoader)
{
    public const string KFilterByAlbumFavorite = "ALBUMFAVORITE";
    public const string KFilterByArtistFavorite = "ARTISTFAVORITE";
    public const string KFilterByGenreFavorite = "GENREFAVORITE";
    public const string KFilterByNeverListened = "NEVERLISTENED";
    public const string KFilterByLive = "LIVE";
    public const string KFilterByBestOf = "BESTOF";
    public const string KFilterByCompilation = "COMPILATION";
    public const string KFilterByAlbum = "ALBUM";
    public const string KFilterByAnniversary = "ANNIVERSARY";

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
        RegisterFilter(KFilterByAnniversary, albums =>
        {
            DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            return albums.Where(a => IsAnniversary(a.ReleaseDate, today));
        });
    }

    internal const int AnniversaryWindowDays = 3;

    /// <summary>
    /// Returns true when the album celebrates a release anniversary (one year or older)
    /// within <see cref="AnniversaryWindowDays"/> days around <paramref name="today"/>.
    /// </summary>
    internal static bool IsAnniversary(DateTime? releaseDate, DateOnly today)
    {
        if (releaseDate is null)
            return false;

        DateOnly release = DateOnly.FromDateTime(releaseDate.Value);

        for (int offset = -AnniversaryWindowDays; offset <= AnniversaryWindowDays; offset++)
        {
            DateOnly candidate = today.AddDays(offset);
            int age = candidate.Year - release.Year;

            if (age >= 1 && release.AddYears(age) == candidate)
                return true;
        }

        return false;
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
            KFilterByAnniversary => ResourceLoader.GetString("albumsViewFilterByAnniversary"),
            _ => ResourceLoader.GetString("albumsViewFilterNone"),
        };
    }
}