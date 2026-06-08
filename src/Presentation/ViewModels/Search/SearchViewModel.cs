using CommunityToolkit.Mvvm.ComponentModel;

namespace Rok.ViewModels.Search;

public partial class SearchViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    public void LoadData(SearchOpenArgs openArgs)
    {
        Guard.NotNull(openArgs, nameof(openArgs));
    }
}
