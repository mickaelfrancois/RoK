using Rok.Logic.ViewModels;

namespace Rok.Logic.Services;

public abstract class GroupCategoryService<TViewModel, TGroupCategory>(ResourceLoader resourceLoader)
    where TGroupCategory : IGroupCategoryViewModel<TViewModel>, new()
{
    protected readonly ResourceLoader ResourceLoader = resourceLoader;
    private readonly Dictionary<string, Func<List<TViewModel>, IEnumerable<TGroupCategory>>> _groupStrategies = new();

    protected abstract void RegisterGroupingStrategies();

    public abstract string GetGroupByLabel(string groupBy);


    public IEnumerable<TGroupCategory> GetGroupedItems(string groupBy, List<TViewModel> items)
    {
        if (_groupStrategies.Count == 0)
            RegisterGroupingStrategies();

        if (string.IsNullOrEmpty(groupBy))
            groupBy = GroupingConstants.None;

        if (_groupStrategies.TryGetValue(groupBy, out Func<List<TViewModel>, IEnumerable<TGroupCategory>>? strategy))
            return strategy(items);

        throw new ArgumentOutOfRangeException(nameof(groupBy), $"Unknown group: '{groupBy}'");
    }


    protected void RegisterStrategy(string key, Func<List<TViewModel>, IEnumerable<TGroupCategory>> strategy)
    {
        _groupStrategies[key] = strategy;
    }


    protected IEnumerable<TGroupCategory> GroupByName<TEntity>(List<TViewModel> items, Func<TViewModel, string> nameSelector, Func<TViewModel, TEntity> sortSelector) where TEntity : IComparable<TEntity>
    {
        IEnumerable<TGroupCategory> selectedItems = items
            .GroupBy(x => StringExtensions.GetNameFirstLetter(nameSelector(x)))
            .Select(x => new TGroupCategory { Title = x.Key, Items = x.OrderBy(sortSelector).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    protected IEnumerable<TGroupCategory> GroupByCountry(List<TViewModel> items, Func<TViewModel, string?> countryCodeSelector, Func<TViewModel, string> sortSelector)
    {
        IEnumerable<TGroupCategory> selectedItems = items
            .GroupBy(x => string.IsNullOrEmpty(countryCodeSelector(x)) ? "#123" : countryCodeSelector(x))
            .Select(x => new TGroupCategory { Title = x.Key!, Items = x.OrderBy(sortSelector).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    protected IEnumerable<TGroupCategory> SortByLastListen(List<TViewModel> items, Func<TViewModel, DateTime?> lastListenSelector)
    {
        List<TViewModel> sortedItems = items.OrderByDescending(lastListenSelector).ToList();

        return new List<TGroupCategory>
        {
            new() {
                Title = string.Empty,
                Items = sortedItems
            }
        };
    }


    protected IEnumerable<TGroupCategory> SortByListenCount(List<TViewModel> items, Func<TViewModel, int> listenCountSelector)
    {
        List<TViewModel> sortedItems = items.OrderByDescending(listenCountSelector).ToList();

        return new List<TGroupCategory>
        {
            new() {
                Title = string.Empty,
                Items = sortedItems
            }
        };
    }


    protected IEnumerable<TGroupCategory> GroupByCreatDate(List<TViewModel> items, Func<TViewModel, DateTime> creatDateSelector)
    {
        DateTime minDate = DateTime.Now.AddYears(-1);

        IEnumerable<TGroupCategory> selectedItems = items
            .OrderByDescending(creatDateSelector)
            .GroupBy(x =>
            {
                DateTime creatDate = creatDateSelector(x);
                return creatDate > minDate ? creatDate.ToString("y") : $"< {minDate:y}";
            })
            .Select(x => new TGroupCategory { Title = x.Key, Items = x.OrderByDescending(creatDateSelector).ToList() });

        return BuildGroupedCollection(selectedItems);
    }


    protected IEnumerable<TGroupCategory> GroupByDecade(List<TViewModel> items, Func<TViewModel, int?> yearSelector, Func<TViewModel, string> sortSelector)
    {
        IOrderedEnumerable<TGroupCategory> selectedItems = items
            .Where(x => yearSelector(x).HasValue)
            .GroupBy(x => (Math.Floor((decimal)yearSelector(x)!.Value / 10) * 10).ToString())
            .Select(x => new TGroupCategory { Title = x.Key, Items = x.OrderBy(sortSelector).ToList() })
            .OrderByDescending(c => c.Title);

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }


    protected IEnumerable<TGroupCategory> GroupByYear(List<TViewModel> items, Func<TViewModel, int?> yearSelector, Func<TViewModel, string> sortSelector)
    {
        IOrderedEnumerable<TGroupCategory> selectedItems = items
            .Where(x => yearSelector(x).HasValue)
            .GroupBy(x => yearSelector(x)?.ToString() ?? string.Empty)
            .Select(x => new TGroupCategory { Title = x.Key, Items = x.OrderBy(sortSelector).ToList() })
            .OrderByDescending(c => c.Title);

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }


    protected static List<TGroupCategory> BuildGroupedCollection(IEnumerable<TGroupCategory> items)
    {
        return [.. items];
    }


    protected static IEnumerable<TGroupCategory> BuildGroupedCollection(IEnumerable<TGroupCategory> items, bool orderByDescending)
    {
        return orderByDescending
            ? items.OrderByDescending(c => c.Title)
            : items.OrderBy(c => c.Title);
    }
}