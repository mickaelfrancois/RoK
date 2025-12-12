namespace Rok.Logic.ViewModels.Tracks;

public class TracksGroupCategoryViewModel : IGroupCategoryViewModel<TrackViewModel>
{
    public string Title { get; set; } = string.Empty;

    public List<TrackViewModel> Items { get; set; } = [];

    public override string ToString() => Title;
}
