using Rok.Application.Interfaces;

namespace Rok.Application.Services.Filters;

public class ArtistsFilter(IResourceService resourceLoader) : FilterService<IFilterableArtist>(resourceLoader)
{
    public const string KFilterByFavoriteArtist = "ARTISTFAVORITE";
    public const string KFilterByGenreFavorite = "GENREFAVORITE";
    public const string KFilterByNeverListened = "NEVERLISTENED";

    protected override void RegisterFilterStrategies()
    {
        RegisterFilter(KFilterByFavoriteArtist, artists => artists.Where(a => a.IsFavorite));
        RegisterFilter(KFilterByGenreFavorite, artists => artists.Where(a => a.IsGenreFavorite));
        RegisterFilter(KFilterByNeverListened, artists => artists.Where(a => a.ListenCount == 0));
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