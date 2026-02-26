namespace Rok.Application.Services.Grouping;

public class TrackGroupResult : IGroupCategory<IGroupableTrack>
{
    public string Title { get; set; } = string.Empty;

    public List<IGroupableTrack> Items { get; set; } = [];
}
