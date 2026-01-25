namespace Rok.Logic.Services;

public abstract class FilterService<TViewModel>
{
    protected readonly ResourceLoader ResourceLoader;
    private readonly Dictionary<string, Func<IEnumerable<TViewModel>, IEnumerable<TViewModel>>> _filterStrategies;

    protected FilterService(ResourceLoader resourceLoader)
    {
        ResourceLoader = resourceLoader;
        _filterStrategies = new Dictionary<string, Func<IEnumerable<TViewModel>, IEnumerable<TViewModel>>>();
    }

    protected abstract void RegisterFilterStrategies();

    public abstract string GetLabel(string filterBy);


    public IEnumerable<TViewModel> Filter(string filterBy, IEnumerable<TViewModel> items)
    {
        if (_filterStrategies.Count == 0)
            RegisterFilterStrategies();

        if (_filterStrategies.TryGetValue(filterBy, out Func<IEnumerable<TViewModel>, IEnumerable<TViewModel>>? strategy))
            return strategy(items);

        return items;
    }


    public IEnumerable<TViewModel> FilterByGenreId(long genreId, IEnumerable<TViewModel> items, Func<TViewModel, long?> genreIdSelector)
    {
        if (genreId == 0)
            return items;

        return items.Where(item => genreIdSelector(item) == genreId);
    }


    public IEnumerable<TViewModel> FilterByTags(List<string> tags, IEnumerable<TViewModel> items, Func<TViewModel, List<string>> tagsSelector)
    {
        if (tags == null || tags.Count == 0)
            return items;

        return items.Where(item => tags.All(t => tagsSelector(item).Contains(t)));
    }


    protected void RegisterFilter(string key, Func<IEnumerable<TViewModel>, IEnumerable<TViewModel>> filter)
    {
        _filterStrategies[key] = filter;
    }


    protected IEnumerable<TViewModel> FilterByFavorite(IEnumerable<TViewModel> items, Func<TViewModel, bool> favoriteSelector)
    {
        return items.Where(favoriteSelector);
    }


    protected IEnumerable<TViewModel> FilterByNeverListened(IEnumerable<TViewModel> items, Func<TViewModel, int> listenCountSelector)
    {
        return items.Where(item => listenCountSelector(item) == 0);
    }


    protected IEnumerable<TViewModel> FilterByCondition(IEnumerable<TViewModel> items, Func<TViewModel, bool> condition)
    {
        return items.Where(condition);
    }
}