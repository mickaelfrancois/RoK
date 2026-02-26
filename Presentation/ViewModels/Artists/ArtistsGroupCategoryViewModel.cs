using Rok.Application.Services.Grouping;
using Rok.ViewModels.Artist;

namespace Rok.ViewModels.Artists;

public class ArtistsGroupCategoryViewModel : IGroupCategory<ArtistViewModel>
{
    public string Title { get; set; } = string.Empty;

    public List<ArtistViewModel> Items { get; set; } = [];

    public override string ToString() => Title;
}
