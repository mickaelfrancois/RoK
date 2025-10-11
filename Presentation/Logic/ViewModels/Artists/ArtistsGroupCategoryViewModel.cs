namespace Rok.Logic.ViewModels.Artists;

public class ArtistsGroupCategoryViewModel
{
    public string Title { get; set; } = string.Empty;

    public List<ArtistViewModel> Items { get; set; } = [];

    public override string ToString() => Title;
}
