namespace Rok.Logic.Services;

public abstract class ViewStateManager(IAppOptions appOptions)
{
    protected readonly IAppOptions AppOptions = appOptions;

    public string GroupBy { get; set; } = string.Empty;

    public List<string> SelectedFilters { get; set; } = [];

    public List<long> SelectedGenreFilters { get; set; } = [];

    protected abstract string GetDefaultGroupBy();

    protected abstract string? GetStoredGroupBy();

    protected abstract void SaveGroupBy(string value);

    protected abstract List<string> GetStoredFilters();

    protected abstract void SaveFilters(List<string> filters);

    protected abstract List<long> GetStoredGenreFilters();

    protected abstract void SaveGenreFilters(List<long> filters);

    public void Load()
    {
        string? storedGroupBy = GetStoredGroupBy();
        GroupBy = string.IsNullOrEmpty(storedGroupBy) ? GetDefaultGroupBy() : storedGroupBy;
        SelectedFilters = GetStoredFilters();
        SelectedGenreFilters = GetStoredGenreFilters();
    }

    public void Save()
    {
        SaveGroupBy(GroupBy);
        SaveFilters(SelectedFilters);
        SaveGenreFilters(SelectedGenreFilters);
    }
}