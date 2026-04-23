namespace Rok.ViewModels.Artist;

internal class ArtistOpenArgs(long artistId)
{
    public long ArtistId { get; } = artistId;
}
