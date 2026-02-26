namespace Rok.Application.Services.Grouping;

public interface IGroupableAlbum : IGroupable
{
    string Name { get; }
    string ArtistName { get; }
    int? Year { get; }
}
