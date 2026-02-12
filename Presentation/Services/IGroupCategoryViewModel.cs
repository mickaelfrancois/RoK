namespace Rok.Services;

public interface IGroupCategoryViewModel<TViewModel>
{
    string Title { get; set; }

    List<TViewModel> Items { get; set; }
}
