using Rok.Application.Services.Grouping;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Tracks;

public class TracksGroupCategoryViewModel : IGroupCategory<TrackViewModel>
{
    public string Title { get; set; } = string.Empty;

    public List<TrackViewModel> Items { get; set; } = [];

    public override string ToString() => Title;
}
