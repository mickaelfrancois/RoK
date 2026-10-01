namespace Rok.Services;

public abstract class ViewStateManager(IAppOptions appOptions)
{
    protected readonly IAppOptions AppOptions = appOptions;

    public string GroupBy { get; set; } = string.Empty;

    public List<string> SelectedFilters { get; set; } = [];

    public List<long> SelectedGenreFilters { get; set; } = [];

    public List<string> SelectedTagFilters { get; set; } = [];

    protected abstract string GetDefaultGroupBy();

    protected abstract string? GetStoredGroupBy();

    protected abstract void SaveGroupBy(string value);

    protected abstract List<string> GetStoredFilters();

    protected abstract void SaveFilters(List<string> filters);

    protected abstract List<long> GetStoredGenreFilters();

    protected abstract List<string> GetStoredTagFilters();

    protected abstract void SaveGenreFilters(List<long> filters);

    protected abstract void SaveTagFilters(List<string> tags);

    public void SaveGridView(bool isGridView) => AppOptions.IsGridView = isGridView;

    public bool GetGridView() => AppOptions.IsGridView;


    public void Load()
    {
        string? storedGroupBy = GetStoredGroupBy();
        GroupBy = string.IsNullOrEmpty(storedGroupBy) ? GetDefaultGroupBy() : storedGroupBy;
        SelectedFilters = GetStoredFilters();
        SelectedGenreFilters = GetStoredGenreFilters();
        SelectedTagFilters = GetStoredTagFilters();
    }

    /// <summary>
    /// Removes in place the selected genre ids that are not in <paramref name="knownGenreIds"/>. The list is shared
    /// with the stored options, so they are cleaned as well.
    /// </summary>
    /// <returns><c>true</c> when at least one id was removed.</returns>
    public bool PruneGenreFilters(IEnumerable<long> knownGenreIds)
    {
        HashSet<long> known = [.. knownGenreIds];

        return SelectedGenreFilters.RemoveAll(id => !known.Contains(id)) > 0;
    }

    /// <summary>
    /// Removes in place the selected tags that are not in <paramref name="knownTags"/> (ordinal comparison). The list
    /// is shared with the stored options, so they are cleaned as well.
    /// </summary>
    /// <returns><c>true</c> when at least one tag was removed.</returns>
    public bool PruneTagFilters(IEnumerable<string> knownTags)
    {
        HashSet<string> known = new(knownTags, StringComparer.Ordinal);

        return SelectedTagFilters.RemoveAll(tag => !known.Contains(tag)) > 0;
    }

    public void Save()
    {
        SaveGroupBy(GroupBy);
        SaveFilters(SelectedFilters);
        SaveGenreFilters(SelectedGenreFilters);
        SaveTagFilters(SelectedTagFilters);
    }
}