namespace Rok.Application.Services.Filters;

public interface IFilterableTrack : IFilterable
{
    bool IsArtistFavorite { get; }
    bool IsAlbumFavorite { get; }
    int Score { get; }
    bool IsLive { get; }
}