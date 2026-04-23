namespace Rok.Application.Services.Grouping;

public interface IGroupableArtist : IGroupable
{
    string Name { get; }
    int? YearMini { get; }
}
