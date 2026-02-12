using Rok.Logic.ViewModels.Albums;
using Rok.Services;

namespace Rok.ViewModels.Albums;


public class AlbumsGroupCategoryViewModel : IGroupCategoryViewModel<AlbumViewModel>
{
    public string Title { get; set; } = string.Empty;

    public List<AlbumViewModel> Items { get; set; } = [];

    public override string ToString() => Title;
}