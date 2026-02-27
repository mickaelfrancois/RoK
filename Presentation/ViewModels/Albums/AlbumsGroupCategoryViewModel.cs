using Rok.Application.Services.Grouping;
using Rok.ViewModels.Album;

namespace Rok.ViewModels.Albums;


public class AlbumsGroupCategoryViewModel : IGroupCategory<AlbumViewModel>
{
    public string Title { get; set; } = string.Empty;

    public List<AlbumViewModel> Items { get; set; } = [];

    public int Count => Items.Count;

    public override string ToString() => Title;
}