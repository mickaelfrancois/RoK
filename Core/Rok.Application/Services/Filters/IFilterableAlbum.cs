namespace Rok.Application.Services.Filters;

public interface IFilterableAlbum : IFilterable
{
    bool IsFavorite { get; }
    bool IsArtistFavorite { get; }
    bool IsAlbumFavorite { get; }
    bool IsLive { get; }
    bool IsBestOf { get; }
    bool IsCompilation { get; }
}
