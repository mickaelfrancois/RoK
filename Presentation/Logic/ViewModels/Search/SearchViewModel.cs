namespace Rok.Logic.ViewModels.Search;

public class SearchViewModel
{
    public void LoadData(SearchOpenArgs openArgs)
    {
        Guard.Against.Null(openArgs, nameof(openArgs));
    }
}
