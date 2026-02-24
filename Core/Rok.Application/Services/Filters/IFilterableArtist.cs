namespace Rok.Application.Services.Filters;

public interface IFilterableArtist : IFilterable
{
    bool IsFavorite { get; }
}
