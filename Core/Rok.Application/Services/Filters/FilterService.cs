using Rok.Application.Interfaces;

namespace Rok.Application.Services.Filters;

public abstract class FilterService<T>(IResourceService resourceLoader) where T : IFilterable
{
    protected readonly IResourceService ResourceLoader = resourceLoader;
    private readonly Dictionary<string, Func<IEnumerable<T>, IEnumerable<T>>> _filterStrategies = new();

    protected abstract void RegisterFilterStrategies();

    public abstract string GetLabel(string filterBy);

    public IEnumerable<T> Filter(string filterBy, IEnumerable<T> items)
    {
        if (_filterStrategies.Count == 0)
            RegisterFilterStrategies();

        if (_filterStrategies.TryGetValue(filterBy, out Func<IEnumerable<T>, IEnumerable<T>>? strategy))
            return strategy(items);

        return items;
    }

    public IEnumerable<T> FilterByGenreId(long genreId, IEnumerable<T> items)
    {
        if (genreId == 0)
            return items;

        return items.Where(item => item.GenreId == genreId);
    }

    public IEnumerable<T> FilterByTags(List<string> tags, IEnumerable<T> items)
    {
        if (tags == null || tags.Count == 0)
            return items;

        return items.Where(item => tags.All(t => item.Tags.Contains(t)));
    }

    protected void RegisterFilter(string key, Func<IEnumerable<T>, IEnumerable<T>> filter)
    {
        _filterStrategies[key] = filter;
    }
}