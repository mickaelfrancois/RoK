namespace Rok.Application.Services.Filters;

public interface IFilterable
{
    long? GenreId { get; }
    int ListenCount { get; }
    bool IsGenreFavorite { get; }
    List<string> Tags { get; }
}
