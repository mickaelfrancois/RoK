namespace Rok.Application.Services.Grouping;

public class ArtistGroupResult : IGroupCategory<IGroupableArtist>
{
    public string Title { get; set; } = string.Empty;

    public List<IGroupableArtist> Items { get; set; } = [];
}
