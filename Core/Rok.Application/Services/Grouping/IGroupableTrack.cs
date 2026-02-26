namespace Rok.Application.Services.Grouping;

public interface IGroupableTrack : IGroupable
{
    string Title { get; }
    string AlbumName { get; }
    string ArtistName { get; }
    string? GenreName { get; }
    int Score { get; }
    int? TrackNumber { get; }
}
